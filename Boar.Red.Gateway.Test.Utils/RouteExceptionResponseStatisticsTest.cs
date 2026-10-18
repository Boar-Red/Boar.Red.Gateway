using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Controllers;
using Boar.Red.Gateway.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test.Utils;

[TestClass]
[DoNotParallelize]
public class RouteExceptionResponseStatisticsTest
{
	////////////////////////////////////////////////
	// @私有方法
	////////////////////////////////////////////////

	#region 私有方法

	private static WebApplication CreateApplication()
	{
		var builder = WebApplication.CreateBuilder();
		builder.Logging.ClearProviders();
		builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
		return builder.Build();
	}

	private static Uri GetApplicationRootUri(WebApplication application)
	{
		var addresses = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
		return new Uri(addresses.Single());
	}

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[TestMethod]
	[DataRow(EndPointResponseForwardMode.WaitForResponseCompleted, false)]
	[DataRow(EndPointResponseForwardMode.WaitForResponseCompleted, true)]
	[DataRow(EndPointResponseForwardMode.ForwardByBytes, false)]
	[DataRow(EndPointResponseForwardMode.ForwardByBytes, true)]
	[DataRow(EndPointResponseForwardMode.ServerSentEvents, false)]
	public async Task RouteRequest_CollectsActualEndPoint500StatusAsync(EndPointResponseForwardMode forwardMode, bool is500Allowed)
	{
		var originalRouterConfig = Config.Router;
		HttpClient? originalEndPointHttpClient = null;
		await using var endPointApplication = CreateApplication();
		endPointApplication.MapGet("/status500", () => Results.Text("endpoint failure", statusCode: 500));
		endPointApplication.MapGet("/status502", () => Results.Text("bad gateway", statusCode: 502));
		await endPointApplication.StartAsync();
		await using var gatewayApplication = CreateApplication();
		gatewayApplication.Run(async context => await Router.RouteInContext(context));
		var routerName = "Exception_Test_" + Guid.NewGuid().ToString("N");
		const string endPointName = "Exception_Test_EndPoint";
		try
		{
			var routerConfig = new RouterConfig()
			{
				ClientRequestHttpHeaderHashKeys = ["Method"],
				ClientRequestHttpHeaderValidKeys = ["Method"],
				EndPointResponseCacheValidHttpStatusCodes = is500Allowed ? [200, 500, 502] : [200],
				RouteRules =
				[
					new RouteRule()
					{
						Name = routerName,
						ClientRequestPaths = ["@" + routerName + "/*"],
						EndPointResponseForwardMode = forwardMode,
						EndPointResponseExceptionProcess = ResponseExceptionProcess.ReturnExceptionInfo,
						ClientResponseCacheMode = ResponseCacheMode.NoneCache,
						EndPoints =
						[
							new EndPoint()
							{
								Name = endPointName,
								PathTemplate = GetApplicationRootUri(endPointApplication) + "#Path#",
								DefaultRouteRate = 1.0
							}
						]
					}
				]
			};
			Config.Router = routerConfig;
			RunTime.UpateOptions(routerConfig);
			Router.UpateRouters(routerConfig);
			originalEndPointHttpClient = ClientResponseCaches.HttpClient;
			ClientResponseCaches.HttpClient = new HttpClient(new HttpClientHandler() { UseProxy = false });
			await gatewayApplication.StartAsync();
			using var client = new HttpClient(new HttpClientHandler() { UseProxy = false })
			{
				BaseAddress = GetApplicationRootUri(gatewayApplication),
				Timeout = TimeSpan.FromSeconds(5)
			};
			var beginTime = DateTime.Now;
			using var response = await client.GetAsync("@" + routerName + "/status500");
			Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
			using var otherResponse = await client.GetAsync("@" + routerName + "/status502");
			await otherResponse.Content.ReadAsStringAsync();

			var deadline = DateTime.UtcNow.AddSeconds(5);
			GetExceptionResponseListResponse? statisticsResponse = null;
			while (DateTime.UtcNow < deadline)
			{
				var result = (JsonResult)new RouteStatisticsController().GetExceptionResponseList(routerName, endPointName);
				statisticsResponse = (GetExceptionResponseListResponse)result.Value!;
				if (statisticsResponse.ExceptionResponseList.Count > 0)
				{
					break;
				}
				await Task.Delay(50);
			}
			Assert.IsNotNull(statisticsResponse);
			Assert.HasCount(1, statisticsResponse.ExceptionResponseList);
			var recorder = statisticsResponse.ExceptionResponseList.Single();
			Assert.AreEqual(500, recorder.ResponseHttpStatusCode);
			Assert.AreEqual(routerName, recorder.RouterName);
			Assert.AreEqual(endPointName, recorder.EndPointName);
			Assert.EndsWith("/status500", recorder.EndPointRequestUrl!);
			Assert.IsTrue(recorder.RouteTime >= beginTime);
			Assert.IsTrue(recorder.IsResponseGetFromEndPoint);
			Assert.AreEqual(!is500Allowed && forwardMode != EndPointResponseForwardMode.ServerSentEvents, recorder.IsRouteFailed);
		}
		finally
		{
			await gatewayApplication.StopAsync();
			await endPointApplication.StopAsync();
			if (originalEndPointHttpClient != null)
			{
				ClientResponseCaches.HttpClient.Dispose();
				ClientResponseCaches.HttpClient = originalEndPointHttpClient;
			}
			Config.Router = originalRouterConfig;
			RunTime.UpateOptions(originalRouterConfig);
			Router.UpateRouters(originalRouterConfig);
		}
	}

	#endregion
}
