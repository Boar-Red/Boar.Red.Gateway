using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test.Utils;

[TestClass]
[DoNotParallelize]
public class RouteRequestPathRoutingTest
{
	////////////////////////////////////////////////
	// @类方法
	////////////////////////////////////////////////

	#region 类方法

	private static WebApplication CreateApplication(bool includeControllers = false)
	{
		var builder = WebApplication.CreateBuilder();
		builder.Logging.ClearProviders();
		builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
		if (includeControllers)
		{
			builder.Services.AddControllersWithViews().AddApplicationPart(typeof(RouteRequestPaths.ConfigController).Assembly);
		}
		var app = builder.Build();
		app.UseWebSockets();
		return app;
	}

	private static Uri RootUri(WebApplication application)
	{
		return new Uri(application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
	}

	#endregion

	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[TestMethod]
	[DataRow("@")]
	[DataRow("~")]
	[DataRow("gw-")]
	[DataRow("")]
	public async Task Routes_HttpAndWebSocketUseOnePrefixAndManagementAndStaticFilesRemainAvailableAsync(string prefix)
	{
		var originalConfig = Config.Router;
		var originalHttpClient = ClientResponseCaches.HttpClient;
		var directory = Path.Combine(Path.GetTempPath(), "Boar.Red.Gateway.RoutePathTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		File.WriteAllText(Path.Combine(directory, "sample.txt"), "static asset");
		using var fileProvider = new PhysicalFileProvider(directory);
		await using var endpointApp = CreateApplication();
		endpointApp.Map("/{*path}", async context =>
		{
			var value = context.Request.Path + context.Request.QueryString;
			if (context.WebSockets.IsWebSocketRequest)
			{
				using var socket = await context.WebSockets.AcceptWebSocketAsync();
				await socket.SendAsync(Encoding.UTF8.GetBytes(value), WebSocketMessageType.Text, true, context.RequestAborted);
				await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", context.RequestAborted);
			}
			else
			{
				await context.Response.WriteAsync(value);
			}
		});
		await endpointApp.StartAsync();
		await using var gatewayApp = CreateApplication(true);
		gatewayApp.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider, RequestPath = "/assets" });
		var options = gatewayApp.CreateRouteRequestPathOptions(prefix);
		gatewayApp.UseRouting();
		gatewayApp.MapControllerRoute("default", "{controller=indexPage}/{action=Index}/{id?}");
		gatewayApp.MapGatewayRouteRequests(options);
		using var routerConfig = new RouterConfig
		{
			RouteRequestPathSpliter = prefix,
			ClientRequestHttpHeaderHashKeys = ["Method"],
			ClientRequestHttpHeaderValidKeys = ["Method"],
			RouteRules =
			[
				new RouteRule
				{
					Name = "Prefix_Test",
					ClientRequestPaths = ["@Stress_Test/*", "*"],
					EndPointResponseExceptionProcess = ResponseExceptionProcess.ReturnExceptionInfo,
					EndPoints = [new EndPoint { Name = "HTTP", DefaultRouteRate = 1, PathTemplate = RootUri(endpointApp) + "#Path#" }]
				}
			]
		};
		try
		{
			Config.Router = routerConfig;
			Router.UpateRouters(routerConfig);
			ClientResponseCaches.HttpClient = new HttpClient(new HttpClientHandler { UseProxy = false });
			await gatewayApp.StartAsync();
			using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = RootUri(gatewayApp), Timeout = TimeSpan.FromSeconds(5) };
			Assert.AreEqual("management controller", await client.GetStringAsync("config/getSystemAdvancedSettings"));
			using var wrongMethod = await client.GetAsync("config/postOnly");
			// 无前缀入口的约束排除内部目录后，框架返回 404；这个请求不能落入转发规则。
			Assert.AreEqual(prefix.Length == 0 ? HttpStatusCode.NotFound : HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
			using var managementPost = await client.PostAsync("config/postOnly", null);
			Assert.AreEqual("management controller", await managementPost.Content.ReadAsStringAsync());
			Assert.AreEqual("static asset", await client.GetStringAsync("assets/sample.txt"));
			Assert.AreEqual("/get/section?key=%40", await client.GetStringAsync(prefix + "Stress_Test/get/section?key=%40"));
			if (prefix.Length > 0)
			{
				Assert.AreEqual("/get?key=1", await client.GetStringAsync(prefix + "/get?key=1"));
			}
			else
			{
				foreach (var path in new[] { "config/missing", "admin/workspace", "assets/missing.js" })
				{
					using var response = await client.GetAsync(path);
					Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, path);
				}
			}
			// 模拟配置热更新，当前入口和解析仍使用启动时的 options。
			routerConfig.RouteRequestPathSpliter = "changed-";
			Assert.AreEqual("/get", await client.GetStringAsync(prefix + "Stress_Test/get"));
			if (prefix.Length > 0)
			{
				using var newPrefixResponse = await client.GetAsync("changed-Stress_Test/get");
				Assert.AreEqual(HttpStatusCode.NotFound, newPrefixResponse.StatusCode);
			}
			routerConfig.RouteRules![0].EndPoints![0].PathTemplate = new UriBuilder(RootUri(endpointApp)) { Scheme = "ws" }.Uri + "#Path#";
			using var socketClient = new ClientWebSocket();
			socketClient.Options.Proxy = null;
			using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			var socketUri = new UriBuilder(new Uri(RootUri(gatewayApp), prefix + "Stress_Test/socket?key=1")) { Scheme = "ws" }.Uri;
			await socketClient.ConnectAsync(socketUri, cancellation.Token);
			var socketBuffer = new byte[1024];
			var socketMessage = await socketClient.ReceiveAsync(new ArraySegment<byte>(socketBuffer), cancellation.Token);
			Assert.AreEqual("/socket?key=1", Encoding.UTF8.GetString(socketBuffer, 0, socketMessage.Count));
			// 断开测试连接；本次验证前缀和数据转发，不依赖网关的关闭握手实现。
			socketClient.Abort();
		}
		finally
		{
			await gatewayApp.StopAsync();
			await endpointApp.StopAsync();
			ClientResponseCaches.HttpClient.Dispose();
			ClientResponseCaches.HttpClient = originalHttpClient;
			Config.Router = originalConfig;
			Router.UpateRouters(originalConfig);
			fileProvider.Dispose();
			var expectedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Boar.Red.Gateway.RoutePathTests")) + Path.DirectorySeparatorChar;
			if (Path.GetFullPath(directory).StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
			{
				Directory.Delete(directory, true);
			}
		}
	}

	#endregion
}
