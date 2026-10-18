using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Utils;
using Boar.Red.Gateway.ViewModels;
using Boar.Red.Service.Models;
using System.Linq;
using System.Text.Json.Nodes;

namespace Boar.Red.Gateway.Extensions.SystemSettings;

public static class SystemAdvancedSettingsInfoFromRouterConfigExtension
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public static SystemAdvancedSettingsInfo ToSystemAdvancedSettingsInfo(this RouterConfig config)
	{
		return new SystemAdvancedSettingsInfo
		{
			AdministratorLoginIpWhitelist = ConfigDocumentUtil.JoinValues(config.ServiceAdministratorConfig?.AdministratorIPAddresses),
			RouteRequestPathSpliter = config.RouteRequestPathSpliter,
			EffectiveRouteRequestPathSpliter = RunTime.RouteRequestPaths.PathSpliter
		};
	}

	public static JsonObject ToRouterConfigDocument(this SystemAdvancedSettingsInfo info, JsonObject document)
	{
		var addresses = ConfigDocumentUtil.SplitValues(info.AdministratorLoginIpWhitelist);
		ConfigDocumentUtil.Require(addresses.All(ConfigDocumentUtil.IsIpAddressOrPattern), "管理员登录 IP 白名单包含无效地址；支持 IPv4、IPv6 和 IPv4 的 * 通配符。");
		if (info.RouteRequestPathSpliter is { } pathSpliter)
		{
			RouteRequestPathOptions.ValidatePathSpliter(pathSpliter);
			ConfigDocumentUtil.Set(document, nameof(RouterConfig.RouteRequestPathSpliter), pathSpliter);
		}
		var administratorConfig = ConfigDocumentUtil.Section<ServiceAdministratorConfig>(document, nameof(RouterConfig.ServiceAdministratorConfig));
		ConfigDocumentUtil.Set(administratorConfig, nameof(ServiceAdministratorConfig.AdministratorIPAddresses), addresses);
		return document;
	}

	#endregion
}
