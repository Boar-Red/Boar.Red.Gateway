using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Test.Endpoint.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test;

[TestClass]
[DoNotParallelize]
public class ServerSentEventsTest
{
	////////////////////////////////////////////////
	// @静态常量
	////////////////////////////////////////////////

	#region 静态常量

	private const int GatewayRequestTimeoutMilliseconds = 3000;

	#endregion


	////////////////////////////////////////////////
	// @静态变量
	////////////////////////////////////////////////

	#region 静态变量

	private static WebApplication? _endPointApplication;
	private static WebApplication? _gatewayApplication;
	private static HttpClient? _gatewayHttpClient;
	private static HttpClient? _originalEndPointHttpClient;
	private static RouterConfig? _originalRouterConfig;

	#endregion


	////////////////////////////////////////////////
	// @类方法
	////////////////////////////////////////////////

	#region 类方法

	[ClassInitialize]
	public static async Task InitializeAsync(TestContext _)
	{
		_originalRouterConfig = Config.Router;
		_endPointApplication = await StartEndPointApplicationAsync();
		var endPointRootUri = GetApplicationRootUri(_endPointApplication);
		using (var endPointHttpClient = new HttpClient(
			new HttpClientHandler()
			{
				UseProxy = false
			})
		{
			BaseAddress = endPointRootUri
		})
		using (var probeResponse = await endPointHttpClient.GetAsync(
			"SseTest/Stream?eventsCount=1",
			HttpCompletionOption.ResponseHeadersRead))
		{
			if (probeResponse.StatusCode != HttpStatusCode.OK
				|| probeResponse.Content.Headers.ContentType?.MediaType != "text/event-stream")
			{
				throw new InvalidOperationException(
					"SSE 测试终点初始化失败，地址：" + endPointRootUri
					+ "，状态码：" + probeResponse.StatusCode
					+ "，Content-Type："
					+ probeResponse.Content.Headers.ContentType?.ToString());
			}
		}

		var routerConfig = CreateRouterConfig(endPointRootUri);
		Config.Router = routerConfig;
		RunTime.UpateOptions(routerConfig);
		_originalEndPointHttpClient = ClientResponseCaches.HttpClient;
		ClientResponseCaches.HttpClient = new HttpClient(
			new HttpClientHandler()
			{
				AllowAutoRedirect = false,
				UseCookies = false,
				UseProxy = false,
				MaxConnectionsPerServer = int.MaxValue
			})
		{
			Timeout = TimeSpan.FromMilliseconds(GatewayRequestTimeoutMilliseconds)
		};
		Router.UpateRouters(routerConfig);

		_gatewayApplication = await StartGatewayApplicationAsync();
		_gatewayHttpClient = new HttpClient(
			new HttpClientHandler()
			{
				UseProxy = false
			})
		{
			BaseAddress = GetApplicationRootUri(_gatewayApplication),
			Timeout = TimeSpan.FromSeconds(10)
		};

		// 预热完整代理链路，避免首次建立连接的抖动影响响应头实时性断言。
		using var warmupResponse = await _gatewayHttpClient.GetAsync(
			"@Sse_Test/Stream?eventsCount=1",
			HttpCompletionOption.ResponseHeadersRead);
		warmupResponse.EnsureSuccessStatusCode();
		await warmupResponse.Content.ReadAsStringAsync();
	}

	[ClassCleanup]
	public static async Task CleanupAsync()
	{
		_gatewayHttpClient?.Dispose();
		_gatewayHttpClient = null;
		if (_originalEndPointHttpClient != null)
		{
			ClientResponseCaches.HttpClient.Dispose();
			ClientResponseCaches.HttpClient = _originalEndPointHttpClient;
			_originalEndPointHttpClient = null;
		}

		if (_gatewayApplication != null)
		{
			await _gatewayApplication.StopAsync();
			await _gatewayApplication.DisposeAsync();
			_gatewayApplication = null;
		}
		if (_endPointApplication != null)
		{
			await _endPointApplication.StopAsync();
			await _endPointApplication.DisposeAsync();
			_endPointApplication = null;
		}
		if (_originalRouterConfig != null)
		{
			Config.Router = _originalRouterConfig;
			RunTime.UpateOptions(_originalRouterConfig);
			Router.UpateRouters(_originalRouterConfig);
			_originalRouterConfig = null;
		}
	}

	private static async Task<WebApplication> StartEndPointApplicationAsync()
	{
		var builder = WebApplication.CreateBuilder();
		builder.Logging.ClearProviders();
		builder.WebHost.ConfigureKestrel(
			kestrelOptions => kestrelOptions.Listen(IPAddress.Loopback, 0));
		builder.Services
			.AddControllers()
			.AddApplicationPart(typeof(SseTestController).Assembly);

		var application = builder.Build();
		application.MapControllers();
		await application.StartAsync();
		return application;
	}

	private static async Task<WebApplication> StartGatewayApplicationAsync()
	{
		var builder = WebApplication.CreateBuilder();
		builder.Logging.ClearProviders();
		builder.WebHost.ConfigureKestrel(
			kestrelOptions => kestrelOptions.Listen(IPAddress.Loopback, 0));

		var application = builder.Build();
		application.Run(
			async httpContext => await Router.RouteInContext(httpContext));
		await application.StartAsync();
		return application;
	}

	private static Uri GetApplicationRootUri(WebApplication application)
	{
		var server = application.Services.GetRequiredService<IServer>();
		var serverAddressesFeature = server.Features.Get<IServerAddressesFeature>();
		var address = serverAddressesFeature?.Addresses.SingleOrDefault()
			?? throw new InvalidOperationException("测试服务启动后没有可用的监听地址。");
		return new Uri(address);
	}

	private static RouterConfig CreateRouterConfig(Uri endPointRootUri)
	{
		var routeRule = new RouteRule()
		{
			Name = "Sse_Test",
			ClientRequestPaths = ["@Sse_Test/*"],
			EndPointRequestTimeoutSeconds = GatewayRequestTimeoutMilliseconds / 1000.0,
			EndPointResponseForwardMode = EndPointResponseForwardMode.ServerSentEvents,
			EndPointResponseForwardBytesCount = 128,
			EndPointResponseExceptionProcess = ResponseExceptionProcess.ReturnExceptionInfo,
			ClientResponseCacheMode = ResponseCacheMode.NoneCache,
			EndPoints =
			[
				new EndPoint()
				{
					Name = "Sse_Test_EndPoint",
					PathTemplate = endPointRootUri + "SseTest/#Path#",
					DefaultRouteRate = 1.0
				}
			]
		};
		using (var clientRequestPathInfo = new ClientRequestPathInfo(
			"Sse_Test",
			"Stream",
			null))
		{
			var endPointRequestUrl = routeRule.CreateEndPointRequestUrlWithClientRequestPath(
				clientRequestPathInfo,
				DateTime.Now,
				out _);
			var expectedEndPointRequestUrl = endPointRootUri + "SseTest/Stream";
			if (!string.Equals(
				endPointRequestUrl,
				expectedEndPointRequestUrl,
				StringComparison.Ordinal))
			{
				throw new InvalidOperationException(
					"SSE 路由生成的终点地址不正确，预期："
					+ expectedEndPointRequestUrl + "，实际：" + endPointRequestUrl);
			}
		}
		return new RouterConfig()
		{
			ClientRequestHttpHeaderHashKeys = ["Method", "Accept", "Last-Event-ID"],
			ClientRequestHttpHeaderValidKeys = ["Method", "Accept"],
			ClientResponseHttpHeaderValidKeys = ["Content-Type", "Cache-Control", "X-Accel-Buffering"],
			EndPointRequestTimeoutSecondsMax = GatewayRequestTimeoutMilliseconds / 1000.0,
			EndPointResponseCacheValidHttpStatusCodes = [(int)HttpStatusCode.OK],
			RouteRules =
			[
				routeRule
			]
		};
	}

	private static HttpClient GetGatewayHttpClient()
	{
		return _gatewayHttpClient
			?? throw new InvalidOperationException("SSE 测试网关尚未初始化。");
	}

	private static async Task<List<string>> ReadEventLinesAsync(
		StreamReader reader,
		CancellationToken cancellationToken)
	{
		var eventLines = new List<string>();
		while (true)
		{
			var eventLine = await reader.ReadLineAsync(cancellationToken);
			if (eventLine == null || eventLine.Length < 1)
			{
				return eventLines;
			}
			eventLines.Add(eventLine);
		}
	}

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[TestMethod]
	public void ServerSentEventsShouldUseSmallerDefaultBuffer()
	{
		var serverSentEventsRouteRule = new RouteRule()
		{
			EndPointResponseForwardModeName = nameof(EndPointResponseForwardMode.ServerSentEvents)
		};
		var forwardByBytesRouteRule = new RouteRule()
		{
			EndPointResponseForwardMode = EndPointResponseForwardMode.ForwardByBytes
		};

		Assert.AreEqual(
			RouteRule.EndPointResponseForwardBytesCountForServerSentEventsDefault,
			serverSentEventsRouteRule.EndPointResponseForwardBytesCount);
		Assert.AreEqual(
			RouteRule.EndPointResponseForwardBytesCountDefault,
			forwardByBytesRouteRule.EndPointResponseForwardBytesCount);
	}

	[TestMethod]
	public async Task ResponseHeadersAndEventsShouldArriveWithoutBufferingAsync()
	{
		const int initialDelayMilliseconds = 1500;
		const int eventIntervalMilliseconds = 700;
		using var testTimeoutCancellationTokenSource = new CancellationTokenSource(
			TimeSpan.FromSeconds(6));
		var stopwatch = Stopwatch.StartNew();

		using var response = await GetGatewayHttpClient().GetAsync(
			"@Sse_Test/Stream?initialDelayMilliseconds=" + initialDelayMilliseconds
			+ "&eventIntervalMilliseconds=" + eventIntervalMilliseconds
			+ "&eventsCount=2",
			HttpCompletionOption.ResponseHeadersRead,
			testTimeoutCancellationTokenSource.Token);
		var responseHeadersElapsedMilliseconds = stopwatch.ElapsedMilliseconds;

		if (response.StatusCode != HttpStatusCode.OK)
		{
			var errorContent = await response.Content.ReadAsStringAsync(
				testTimeoutCancellationTokenSource.Token);
			Assert.Fail(
				"预期 SSE 网关返回 200，实际状态码："
				+ response.StatusCode + "，响应内容：" + errorContent);
		}
		Assert.AreEqual("text/event-stream", response.Content.Headers.ContentType?.MediaType);
		Assert.IsTrue(response.Headers.CacheControl?.NoCache == true);
		Assert.IsTrue(response.Headers.CacheControl?.NoTransform == true);
		Assert.IsTrue(
			response.Headers.TryGetValues("X-Accel-Buffering", out var bufferingHeaderValues)
			&& bufferingHeaderValues.Contains("no"));
		Assert.IsTrue(
			responseHeadersElapsedMilliseconds < initialDelayMilliseconds - 250,
			"网关应在首个事件生成前立即发送 SSE 响应头，实际耗时："
			+ responseHeadersElapsedMilliseconds + "ms。");

		await using var responseStream = await response.Content.ReadAsStreamAsync(
			testTimeoutCancellationTokenSource.Token);
		using var reader = new StreamReader(responseStream);
		var firstEventLines = await ReadEventLinesAsync(
			reader,
			testTimeoutCancellationTokenSource.Token);
		var firstEventElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
		var secondEventLines = await ReadEventLinesAsync(
			reader,
			testTimeoutCancellationTokenSource.Token);
		var secondEventElapsedMilliseconds = stopwatch.ElapsedMilliseconds;

		CollectionAssert.Contains(firstEventLines, "data: sequence=1;lastEventId=");
		CollectionAssert.Contains(secondEventLines, "data: sequence=2;lastEventId=");
		Assert.IsTrue(
			secondEventElapsedMilliseconds - firstEventElapsedMilliseconds
			>= eventIntervalMilliseconds - 150,
			"两个事件应逐条实时到达，不能在流结束时一次性返回。");
	}

	[TestMethod]
	public async Task StreamShouldContinueAfterRequestTimeoutAsync()
	{
		const int eventIntervalMilliseconds = GatewayRequestTimeoutMilliseconds + 700;
		using var testTimeoutCancellationTokenSource = new CancellationTokenSource(
			TimeSpan.FromSeconds(10));
		var stopwatch = Stopwatch.StartNew();

		using var response = await GetGatewayHttpClient().GetAsync(
			"@Sse_Test/Stream?eventIntervalMilliseconds=" + eventIntervalMilliseconds
			+ "&eventsCount=2",
			HttpCompletionOption.ResponseHeadersRead,
			testTimeoutCancellationTokenSource.Token);
		await using var responseStream = await response.Content.ReadAsStreamAsync(
			testTimeoutCancellationTokenSource.Token);
		using var reader = new StreamReader(responseStream);
		_ = await ReadEventLinesAsync(reader, testTimeoutCancellationTokenSource.Token);
		var secondEventLines = await ReadEventLinesAsync(
			reader,
			testTimeoutCancellationTokenSource.Token);

		CollectionAssert.Contains(secondEventLines, "data: sequence=2;lastEventId=");
		Assert.IsTrue(
			stopwatch.ElapsedMilliseconds > GatewayRequestTimeoutMilliseconds,
			"SSE 连接应在响应头成功后跨过普通请求超时继续传输。");
	}

	[TestMethod]
	public async Task LastEventIdShouldBeForwardedAsync()
	{
		const string lastEventId = "event-42";
		using var testTimeoutCancellationTokenSource = new CancellationTokenSource(
			TimeSpan.FromSeconds(4));
		using var request = new HttpRequestMessage(
			HttpMethod.Get,
			"@Sse_Test/Stream?eventsCount=1");
		request.Headers.Accept.ParseAdd("text/event-stream");
		request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);

		using var response = await GetGatewayHttpClient().SendAsync(
			request,
			HttpCompletionOption.ResponseHeadersRead,
			testTimeoutCancellationTokenSource.Token);
		await using var responseStream = await response.Content.ReadAsStreamAsync(
			testTimeoutCancellationTokenSource.Token);
		using var reader = new StreamReader(responseStream);
		var eventLines = await ReadEventLinesAsync(
			reader,
			testTimeoutCancellationTokenSource.Token);

		CollectionAssert.Contains(
			eventLines,
			"data: sequence=1;lastEventId=" + lastEventId);
	}

	[TestMethod]
	public async Task NoContentResponseShouldBeForwardedAsync()
	{
		using var response = await GetGatewayHttpClient().GetAsync(
			"@Sse_Test/NoContentResponse");

		Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
	}

	[TestMethod]
	public async Task InvalidContentTypeShouldReturnBadGatewayAsync()
	{
		using var response = await GetGatewayHttpClient().GetAsync(
			"@Sse_Test/InvalidContentType");

		Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
	}

	[TestMethod]
	public async Task ClientCancellationShouldCancelEndPointRequestAsync()
	{
		var connectionId = Guid.NewGuid().ToString("N");
		SseTestController.ResetConnectionAbortState(connectionId);
		using var clientCancellationTokenSource = new CancellationTokenSource();
		using var testTimeoutCancellationTokenSource = new CancellationTokenSource(
			TimeSpan.FromSeconds(6));
		using var request = new HttpRequestMessage(
			HttpMethod.Get,
			"@Sse_Test/Stream?eventsCount=1&holdOpenMilliseconds=30000"
			+ "&connectionId=" + connectionId);

		using var response = await GetGatewayHttpClient().SendAsync(
			request,
			HttpCompletionOption.ResponseHeadersRead,
			clientCancellationTokenSource.Token);
		await using var responseStream = await response.Content.ReadAsStreamAsync(
			testTimeoutCancellationTokenSource.Token);
		using var reader = new StreamReader(responseStream);
		_ = await ReadEventLinesAsync(reader, testTimeoutCancellationTokenSource.Token);

		clientCancellationTokenSource.Cancel();
		response.Dispose();
		var waitForAbortDeadline = DateTime.UtcNow.AddSeconds(5);
		while (!SseTestController.IsConnectionAborted(connectionId)
			&& DateTime.UtcNow < waitForAbortDeadline)
		{
			await Task.Delay(50);
		}

		Assert.IsTrue(
			SseTestController.IsConnectionAborted(connectionId),
			"客户端断开 SSE 后，网关应及时取消上游请求。");
	}

	#endregion
}
