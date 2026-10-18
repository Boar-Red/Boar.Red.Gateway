using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Utils;
using Boar.Red.Gateway.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Boar.Red.Gateway.Extensions.SystemSettings;

public static class SystemRouteRuleInfoFromRouteRuleExtension
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	private static string ToStagedTrafficControl(RouteRateControllInfo[]? stages)
	{
		var begin = 0.0;
		var values = new List<string>();
		foreach (var stage in stages ?? [])
		{
			var end = begin + stage.DurationSeconds;
			values.Add(FormattableString.Invariant($"{begin:G}-{end:G}秒，{stage.RouteRate * 100:G}%"));
			begin = end;
		}
		return string.Join("；", values);
	}

	private static RouteRateControllInfo[] ParseStages(string? text, double defaultRate)
	{
		var stages = new List<RouteRateControllInfo>();
		var previousEnd = 0.0;
		foreach (var value in (text ?? string.Empty).Split([';', '；'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
		{
			var parts = value.Replace('，', ',').Split(',', StringSplitOptions.TrimEntries);
			ConfigDocumentUtil.Require(parts.Length == 2, "分阶段流量格式应为：0-3秒，10%；3-6秒，50%。");
			var range = parts[0].TrimEnd('秒').Split('-', StringSplitOptions.TrimEntries);
			ConfigDocumentUtil.Require(range.Length == 2 && parts[1].EndsWith('%'), "分阶段流量时间范围或百分比格式无效。");
			ConfigDocumentUtil.Require(double.TryParse(range[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var begin)
				&& double.IsFinite(begin) && begin >= previousEnd, "分阶段流量的起始时间无效，或与上一阶段重叠。");
			ConfigDocumentUtil.Require(double.TryParse(range[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var end)
				&& double.IsFinite(end) && end > begin && end < (DateTime.MaxValue - DateTime.Now).TotalSeconds, "分阶段流量的结束时间无效。");
			ConfigDocumentUtil.Require(double.TryParse(parts[1][..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
				&& double.IsFinite(percent) && percent is >= 0 and <= 100, "分阶段流量百分比必须在 0 到 100 之间。");
			if (begin > previousEnd)
			{
				stages.Add(new RouteRateControllInfo { DurationSeconds = begin - previousEnd, RouteRate = defaultRate });
			}
			stages.Add(new RouteRateControllInfo { DurationSeconds = end - begin, RouteRate = percent / 100 });
			previousEnd = end;
		}
		return stages.ToArray();
	}

	private static EndPointRoutePolicy ToAllocationPolicy(string? value)
	{
		return value switch
		{
			"主从模式" => EndPointRoutePolicy.MasterSlave,
			"随机模式" => EndPointRoutePolicy.Random,
			"手动模式" => EndPointRoutePolicy.Manual,
			_ when Enum.TryParse<EndPointRoutePolicy>(value, true, out var policy) && Enum.IsDefined(policy) => policy,
			_ => throw new ArgumentException("不支持的路由分配模式。")
		};
	}

	private static ResponseCacheMode ToCacheMode(string? value)
	{
		return value switch
		{
			"不使用缓存" => ResponseCacheMode.NoneCache,
			"返回缓存并自动更新" => ResponseCacheMode.ReturnCache_AutoUpdateCache,
			"返回终点响应并保存缓存" => ResponseCacheMode.ReturnEndPointResponse_AndSaveToCache,
			"返回缓存并手动更新" => ResponseCacheMode.ReturnCache_UpdateCacheManual,
			_ when Enum.TryParse<ResponseCacheMode>(value, true, out var mode) && Enum.IsDefined(mode) => mode,
			_ => throw new ArgumentException("不支持的响应缓存模式。")
		};
	}

	public static SystemRouteRuleInfo ToSystemRouteRuleInfo(this RouteRule rule, long identity)
	{
		return new SystemRouteRuleInfo
		{
			Identity = identity,
			Name = rule.Name,
			AllocationMode = rule.EndPointRoutePolicy switch
			{
				EndPointRoutePolicy.MasterSlave => "主从模式",
				EndPointRoutePolicy.Random => "随机模式",
				_ => "手动模式"
			},
			MatchPath = ConfigDocumentUtil.JoinValues(rule.ClientRequestPaths),
			TimeoutSeconds = rule.EndPointRequestTimeoutSeconds,
			CacheMode = rule.ClientResponseCacheMode switch
			{
				ResponseCacheMode.ReturnCache_AutoUpdateCache => "返回缓存并自动更新",
				ResponseCacheMode.ReturnEndPointResponse_AndSaveToCache => "返回终点响应并保存缓存",
				ResponseCacheMode.ReturnCache_UpdateCacheManual => "返回缓存并手动更新",
				_ => "不使用缓存"
			},
			CacheRefreshSeconds = rule.ClientResponseCacheUpdateIntervalSeconds,
			StagedTrafficControl = ToStagedTrafficControl(rule.EndPoints?.FirstOrDefault()?.RouteRateControllInfes),
			EndPoints = rule.EndPoints?.Select((endPoint, index) => new SystemRouteEndPointInfo
			{
				Identity = index + 1,
				Name = endPoint.Name,
				Address = endPoint.PathTemplate,
				IsEnable = endPoint.State is not EndPointState.Disable and not EndPointState.Disabling,
				TrafficPercent = endPoint.DefaultRouteRate * 100
			}).ToArray() ?? []
		};
	}

	public static JsonObject ToRouterConfigDocument(this SaveRouteRulesRequest request, JsonObject document)
	{
		ConfigDocumentUtil.Require(request.RouteRules != null, "必须提供完整路由规则数组；空数组表示清空规则。");
		var originalRules = document[nameof(RouterConfig.RouteRules)] as JsonArray ?? [];
		var rules = new JsonArray();
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var identities = new HashSet<long>();
		foreach (var info in request.RouteRules!)
		{
			ConfigDocumentUtil.Require(info != null && !string.IsNullOrWhiteSpace(info.Name), "路由规则名称不能为空。");
			ConfigDocumentUtil.Require(names.Add(info!.Name!.Trim()), "路由规则名称重复。");
			ConfigDocumentUtil.Require(info.Identity <= 0 || identities.Add(info.Identity), "路由规则标识重复。");
			ConfigDocumentUtil.RequireNonNegative(info.TimeoutSeconds, "路由超时时长");
			ConfigDocumentUtil.RequireNonNegative(info.CacheRefreshSeconds, "路由缓存更新间隔");
			var paths = ConfigDocumentUtil.SplitValues(info.MatchPath);
			ConfigDocumentUtil.Require(paths.Length > 0, "路由匹配路径不能为空。");
			ConfigDocumentUtil.Require(info.EndPoints != null, "必须提供路由终点数组。");
			var originalRule = info.Identity > 0 && info.Identity <= originalRules.Count ? originalRules[(int)info.Identity - 1] as JsonObject : null;
			originalRule ??= originalRules.OfType<JsonObject>().FirstOrDefault(rule =>
				string.Equals(ConfigDocumentUtil.Get<string>(rule, nameof(RouteRule.Name)), info.Name, StringComparison.OrdinalIgnoreCase));
			var rule = originalRule == null ? ConfigDocumentUtil.ToDocument(new RouteRule()) : ConfigDocumentUtil.Parse(originalRule.ToJsonString());
			var originalStagesText = originalRule?.Deserialize<RouteRule>(ConfigDocumentUtil.JsonOptions)?.ToSystemRouteRuleInfo(info.Identity).StagedTrafficControl ?? string.Empty;
			var stagesChanged = !string.Equals(info.StagedTrafficControl ?? string.Empty, originalStagesText, StringComparison.Ordinal);
			var originalEndPoints = rule[nameof(RouteRule.EndPoints)] as JsonArray ?? [];
			var endPoints = new JsonArray();
			var endPointNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var endPointIdentities = new HashSet<long>();
			foreach (var endPointInfo in info.EndPoints!)
			{
				ConfigDocumentUtil.Require(endPointInfo != null && !string.IsNullOrWhiteSpace(endPointInfo.Address), "终点地址不能为空。");
				var address = endPointInfo!.Address!.Trim();
				ConfigDocumentUtil.Require(Uri.TryCreate(address, UriKind.Absolute, out var uri)
					&& uri.Scheme is "http" or "https" or "ws" or "wss", "终点地址必须使用 http、https、ws 或 wss 协议。");
				ConfigDocumentUtil.Require(double.IsFinite(endPointInfo.TrafficPercent) && endPointInfo.TrafficPercent is >= 0 and <= 100, "终点流量百分比必须在 0 到 100 之间。");
				ConfigDocumentUtil.Require(endPointInfo.Identity <= 0 || endPointIdentities.Add(endPointInfo.Identity), "终点标识重复。");
				var originalEndPoint = endPointInfo.Identity > 0 && endPointInfo.Identity <= originalEndPoints.Count
					? originalEndPoints[(int)endPointInfo.Identity - 1] as JsonObject : null;
				originalEndPoint ??= originalEndPoints.OfType<JsonObject>().FirstOrDefault(endPoint =>
					string.Equals(ConfigDocumentUtil.Get<string>(endPoint, nameof(EndPoint.Name)), endPointInfo.Name, StringComparison.OrdinalIgnoreCase));
				var endPoint = originalEndPoint == null ? ConfigDocumentUtil.ToDocument(new EndPoint()) : ConfigDocumentUtil.Parse(originalEndPoint.ToJsonString());
				var name = endPointInfo.Name?.Trim();
				if (string.IsNullOrEmpty(name))
				{
					name = ConfigDocumentUtil.Get<string>(endPoint, nameof(EndPoint.Name));
					if (string.IsNullOrEmpty(name))
					{
						name = address;
					}
				}
				ConfigDocumentUtil.Require(endPointNames.Add(name), "同一路由内终点名称不能重复。");
				ConfigDocumentUtil.Set(endPoint, nameof(EndPoint.Name), name);
				ConfigDocumentUtil.Set(endPoint, nameof(EndPoint.PathTemplate), address);
				ConfigDocumentUtil.Set(endPoint, nameof(EndPoint.DefaultRouteRate), endPointInfo.TrafficPercent / 100);
				var originalState = ConfigDocumentUtil.Get<string>(endPoint, nameof(EndPoint.StateName));
				var wasEnabled = originalState != nameof(EndPointState.Disable) && originalState != nameof(EndPointState.Disabling);
				if (originalEndPoint == null || wasEnabled != endPointInfo.IsEnable)
				{
					ConfigDocumentUtil.Set(endPoint, nameof(EndPoint.StateName), endPointInfo.IsEnable ? nameof(EndPointState.Enabling) : nameof(EndPointState.Disable));
				}
				if (stagesChanged || originalEndPoint == null)
				{
					ConfigDocumentUtil.Set(endPoint, nameof(EndPoint.RouteRateControllInfes), ParseStages(info.StagedTrafficControl, endPointInfo.TrafficPercent / 100));
				}
				endPoints.Add(endPoint);
			}
			ConfigDocumentUtil.Set(rule, nameof(RouteRule.OrderIndex), rules.Count);
			ConfigDocumentUtil.Set(rule, nameof(RouteRule.Name), info.Name.Trim());
			ConfigDocumentUtil.Set(rule, nameof(RouteRule.ClientRequestPaths), paths);
			ConfigDocumentUtil.Set(rule, nameof(RouteRule.EndPointRoutePolicyName), ToAllocationPolicy(info.AllocationMode).ToString());
			ConfigDocumentUtil.Set(rule, nameof(RouteRule.EndPointRequestTimeoutSeconds), info.TimeoutSeconds);
			ConfigDocumentUtil.Set(rule, nameof(RouteRule.ClientResponseCacheModeName), ToCacheMode(info.CacheMode).ToString());
			ConfigDocumentUtil.Set(rule, nameof(RouteRule.ClientResponseCacheUpdateIntervalSeconds), info.CacheRefreshSeconds);
			rule[nameof(RouteRule.EndPoints)] = endPoints;
			rules.Add(rule);
		}
		document[nameof(RouterConfig.RouteRules)] = rules;
		return document;
	}

	#endregion
}
