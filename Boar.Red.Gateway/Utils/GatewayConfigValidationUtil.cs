using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Defines.ViewModels;
using Boar.Red.Service.Models;
using Boar.Red.Service.WebService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Boar.Red.Gateway.Utils;

public static class GatewayConfigValidationUtil
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	private static void ValidateEnum<T>(JsonObject document, string name) where T : struct, Enum
	{
		if (document[name] != null)
		{
			ConfigDocumentUtil.Require(Enum.TryParse<T>(ConfigDocumentUtil.Get<string>(document, name), true, out var value)
				&& Enum.IsDefined(value), name + "包含不支持的值。");
		}
	}

	public static void ValidateRouterDocument(JsonObject document)
	{
		ConfigDocumentUtil.Require(document[nameof(RouterConfig.WebServiceConfig)] is JsonObject
			&& document[nameof(RouterConfig.ServiceAdministratorConfig)] is JsonObject
			&& document[nameof(RouterConfig.RouteRules)] is JsonArray, "必须上传包含 WebServiceConfig、ServiceAdministratorConfig 和 RouteRules 的完整网关配置。");
		if (document.ContainsKey(nameof(RouterConfig.RouteRequestPathSpliter)))
		{
			RouteRequestPathOptions.ValidatePathSpliter(ConfigDocumentUtil.Get<string>(document, nameof(RouterConfig.RouteRequestPathSpliter)));
		}
		// 逐个反序列化普通配置属性，避免直接创建 RouterConfig 导致注册文件监听器。
		foreach (var property in typeof(RouterConfig).GetProperties())
		{
			if (property.CanWrite && property.GetCustomAttribute<JsonIgnoreAttribute>() == null && document.TryGetPropertyValue(property.Name, out var value))
			{
				ConfigDocumentUtil.Require(value != null || !property.PropertyType.IsValueType, property.Name + "不能为 null。");
				value?.Deserialize(property.PropertyType, ConfigDocumentUtil.JsonOptions);
			}
		}
		var web = ConfigDocumentUtil.Get<WebServiceConfig>(document, nameof(RouterConfig.WebServiceConfig))!;
		ConfigDocumentUtil.ValidateBindAddress(web.HttpIPAddress);
		ConfigDocumentUtil.ValidateBindAddress(web.HttpsIPAddress);
		ConfigDocumentUtil.Require(web.HttpPort is >= 0 and <= 65535 && web.HttpsPort is >= 0 and <= 65535, "监听端口超出有效范围。");
		ConfigDocumentUtil.Require(web.HttpRequestBodySizeInMBMax >= 0 && web.HttpRequestBodySizeInMBMax <= long.MaxValue / (1024 * 1024), "请求大小超出有效范围。");
		foreach (var certificate in web.HttpsCertificateFileInfes ?? [])
		{
			ConfigDocumentUtil.Require(certificate != null && !string.IsNullOrWhiteSpace(certificate.CertificateFilePath), "证书文件配置无效。");
		}
		var admin = ConfigDocumentUtil.Get<ServiceAdministratorConfig>(document, nameof(RouterConfig.ServiceAdministratorConfig))!;
		ConfigDocumentUtil.Require((admin.AdministratorIPAddresses ?? []).All(address => address != null && ConfigDocumentUtil.IsIpAddressOrPattern(address)), "管理员 IP 白名单无效。");
		var accounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var administrator in admin.Administrators ?? [])
		{
			ConfigDocumentUtil.Require(administrator != null && !string.IsNullOrWhiteSpace(administrator.Account)
				&& !string.IsNullOrWhiteSpace(administrator.Password) && accounts.Add(administrator.Account), "管理员账号、密码无效或账号重复。");
		}
		ConfigDocumentUtil.Require(admin.Administrators?.Any(administrator => administrator.IsValid) == true, "配置中至少保留一个未过期、可登录的管理员。");
		var metadataAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var metadataIdentities = new HashSet<long>();
		foreach (var metadata in ConfigDocumentUtil.Get<Boar.Red.Gateway.Models.AdministratorMetadataInfo[]>(document, nameof(RouterConfig.AdministratorMetadataInfes)) ?? [])
		{
			ConfigDocumentUtil.Require(metadata != null && metadata.Identity is > 0 and <= 9007199254740991
				&& metadataIdentities.Add(metadata.Identity) && !string.IsNullOrWhiteSpace(metadata.Name)
				&& metadata.Account != null && accounts.Contains(metadata.Account) && metadataAccounts.Add(metadata.Account), "管理员显示信息无效或标识重复。");
		}
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var route in ((JsonArray)document[nameof(RouterConfig.RouteRules)]!).OfType<JsonNode>())
		{
			ConfigDocumentUtil.Require(route is JsonObject, "路由规则不能为 null 或非对象。");
			var ruleDocument = (JsonObject)route;
			ValidateEnum<EndPointRoutePolicy>(ruleDocument, nameof(RouteRule.EndPointRoutePolicyName));
			ValidateEnum<ResponseCacheMode>(ruleDocument, nameof(RouteRule.ClientResponseCacheModeName));
			ValidateEnum<EndPointResponseForwardMode>(ruleDocument, nameof(RouteRule.EndPointResponseForwardModeName));
			ValidateEnum<ResponseExceptionProcess>(ruleDocument, nameof(RouteRule.EndPointResponseExceptionProcessName));
			var rule = ruleDocument.Deserialize<RouteRule>(ConfigDocumentUtil.JsonOptions)!;
			ConfigDocumentUtil.Require(!string.IsNullOrWhiteSpace(rule.Name) && names.Add(rule.Name), "路由规则名称为空或重复。");
			ConfigDocumentUtil.Require(rule.ClientRequestPaths?.Length > 0 && rule.ClientRequestPaths.All(path => !string.IsNullOrWhiteSpace(path)), "路由匹配路径无效。");
			ConfigDocumentUtil.RequireNonNegative(rule.EndPointRequestTimeoutSeconds, "路由超时时长");
			ConfigDocumentUtil.RequireNonNegative(rule.ClientResponseCacheUpdateIntervalSeconds, "路由缓存更新间隔");
			ConfigDocumentUtil.Require(rule.EndPoints != null && rule.EndPoints.All(endPoint => endPoint != null), "路由终点数组无效。");
			var endPointNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var endPoint in rule.EndPoints!)
			{
				ConfigDocumentUtil.Require(!string.IsNullOrWhiteSpace(endPoint.Name) && endPointNames.Add(endPoint.Name), "终点名称为空或重复。");
				ConfigDocumentUtil.Require(Uri.TryCreate(endPoint.PathTemplate, UriKind.Absolute, out var uri)
					&& uri.Scheme is "http" or "https" or "ws" or "wss", "终点地址无效。");
				ConfigDocumentUtil.RequireNonNegative(endPoint.DefaultRouteRate, "终点流量比例");
				foreach (var stage in endPoint.RouteRateControllInfes ?? [])
				{
					ConfigDocumentUtil.Require(stage != null && double.IsFinite(stage.DurationSeconds) && stage.DurationSeconds > 0
						&& double.IsFinite(stage.RouteRate) && stage.RouteRate >= 0, "终点分阶段流量配置无效。");
				}
			}
			foreach (var endPointDocument in (ruleDocument[nameof(RouteRule.EndPoints)] as JsonArray ?? []).OfType<JsonObject>())
			{
				ValidateEnum<EndPointState>(endPointDocument, nameof(EndPoint.StateName));
			}
		}
		ConfigDocumentUtil.Require(((JsonArray)document[nameof(RouterConfig.RouteRules)]!).All(rule => rule is JsonObject), "路由规则数组包含空值。");
		if (document[nameof(RouterConfig.ClientResponseCompressionMethod)] != null)
		{
			var method = ConfigDocumentUtil.Get<string>(document, nameof(RouterConfig.ClientResponseCompressionMethod))?.ToLowerInvariant();
			ConfigDocumentUtil.Require(method is "none" or "auto" or "br" or "gzip" or "deflate", "响应压缩算法无效。");
		}
		if (document[nameof(RouterConfig.EndPointResponseCacheValidHttpStatusCodes)] != null)
		{
			ConfigDocumentUtil.Require(ConfigDocumentUtil.Get<int[]>(document, nameof(RouterConfig.EndPointResponseCacheValidHttpStatusCodes))!
				.All(code => code is >= 100 and <= 599), "缓存 HTTP 状态码超出有效范围。");
		}
		if (document[nameof(RouterConfig.ClientResponseCacheAutoUpdateIntervalSeconds)] != null)
		{
			ConfigDocumentUtil.Require(ConfigDocumentUtil.Get<double>(document, nameof(RouterConfig.ClientResponseCacheAutoUpdateIntervalSeconds)) > 0, "缓存更新检查间隔必须大于 0。");
		}
	}

	public static IpAddressBlockRecord[] ToValidatedBlockRecords(IpAddressBlockRecord[]? records)
	{
		ConfigDocumentUtil.Require(records != null, "必须提供完整 IP 黑名单数组；空数组表示清空黑名单。");
		var keys = new HashSet<(string, string)>();
		return records!.Select(record =>
		{
			ConfigDocumentUtil.Require(record != null && IPAddress.TryParse(record.TargetIpAddress, out _)
				&& !string.IsNullOrWhiteSpace(record.BlockSourceName) && record.BlockEndTime > record.BlockBeginTime, "IP、封禁来源或封禁时间无效。");
			var ip = IPAddress.Parse(record!.TargetIpAddress).ToString();
			var source = record.BlockSourceName.Trim();
			ConfigDocumentUtil.Require(keys.Add((ip, source)), "相同来源的 IP 封禁记录重复。");
			return new IpAddressBlockRecord
			{
				TargetIpAddress = ip,
				BlockSourceName = source,
				BlockBeginTime = record.BlockBeginTime,
				BlockEndTime = record.BlockEndTime,
				BlockRemark = record.BlockRemark
			};
		}).ToArray();
	}

	#endregion
}
