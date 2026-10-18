using BaoXia.Utils.Interfaces;
using BaoXia.Utils.Models;
using Boar.Red.Gateway.Client.ViewModels;
using Boar.Red.Service.BrService;
using Boar.Red.Service.BrService.Middlewares;
using Boar.Red.Service.BrService.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ServiceInfo = Boar.Red.Gateway.Defines.Constants.ServiceInfo;

namespace Boar.Red.Gateway.Client.Extensions;

/// <summary>
/// BrGatewayService扩展方法，用于向容器配置当前服务的实例。
/// </summary>
public static class BrGatewayServiceExtension
{
	////////////////////////////////////////////////
	// @类方法
	////////////////////////////////////////////////

	#region 类方法

	public static WebApplicationBuilder ConfigureBrGatewayService(this WebApplicationBuilder applicationBuilder,
		string? groupName, string? serviceUri, ServiceDiscoverer? serviceDiscoverer,
		string? applicationIdentity, string? applicationPassword, string? applicationAesKey,
		Func<BrGatewayServiceClientConfig>? toGetClientConfig,
		Func<int, ClientIpInfo, ServiceAdministratorLoginRequest>? toGetServiceAdministratorLoginRequest,
		ILogFile? logFile, ILogFile? exceptionLogFile, ILogFile? warningLogFile, ILogFile? bxServiceLogFile, ILogFile? gatewayLogFile,
		string serviceName = ServiceInfo.ServiceName)
	{
		////////////////////////////////////////////////
		// 初始化单例的服务中间件：
		////////////////////////////////////////////////
		applicationBuilder.Services.AddSingleton<BrGatewayService>((serviceMiddlewareProvider) =>
		{
			return new BrGatewayService(groupName, serviceUri, serviceDiscoverer,
				applicationIdentity, applicationPassword, applicationAesKey, toGetClientConfig, toGetServiceAdministratorLoginRequest,
				logFile, exceptionLogFile, warningLogFile, bxServiceLogFile, gatewayLogFile, serviceName);
		});
		return applicationBuilder;
	}

	public static WebApplicationBuilder ConfigureBrGatewayService(this WebApplicationBuilder applicationBuilder,
		string? groupName, Func<BrGatewayServiceClientConfig?>? toGetClientConfig,
		Func<int, ClientIpInfo, ServiceAdministratorLoginRequest>? toGetServiceAdministratorLoginRequest,
		ILogFile? logFile, ILogFile? exceptionLogFile, ILogFile? warningLogFile, ILogFile? bxServiceLogFile, ILogFile? gatewayLogFile,
		string serviceName = ServiceInfo.ServiceName)
	{
		////////////////////////////////////////////////
		// 初始化单例的服务中间件：
		////////////////////////////////////////////////
		applicationBuilder.Services.AddSingleton<BrGatewayService>((serviceMiddlewareProvider) =>
		{
			var bxService = serviceMiddlewareProvider.GetRequiredService<BrServiceMiddleware>();
			{ }
			return new BrGatewayService(groupName, null, bxService.ServiceDiscoverer, null, null, null, toGetClientConfig,
				toGetServiceAdministratorLoginRequest, logFile, exceptionLogFile, warningLogFile, bxServiceLogFile, gatewayLogFile,
				serviceName);
		});
		return applicationBuilder;
	}

	#endregion
}
