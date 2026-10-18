using BaoXia.Utils;
using BaoXia.Utils.Extensions;
using BaoXia.Utils.Models;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Extensions;
using Boar.Red.Gateway.LogFiles;
using Boar.Red.Gateway.Models;
using Boar.Red.Gateway.Utils;
using Boar.Red.Gateway.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway;

/// <summary>
/// 默认构造函数。
/// </summary>
public class Router(RouteRule RouteRule) : IDisposable
{
	////////////////////////////////////////////////
	// @静态常量
	////////////////////////////////////////////////

	public static readonly string RouterName_All = "All";

	public static readonly string EndPointName_All = "All";

	public const int WebSockets_RequestBodyBufferSizeInByte = 1024;

	static Router[]? _routers;

	static readonly ClientResponseCaches _clientResponseCaches = new();

	static RouteStatisticsInfoRecorder[]? _responseDurationRankList = null;
	static DateTime _lastTimeOfSendMailToWarningResponseDurationOverMax = DateTime.Now;

	static readonly ConcurrentQueue<RouteStatisticsInfoRecorder> _routeStatisticsInfoRecorderCollectQueue = new();

	static readonly ConcurrentDictionary<string, RouterStatisticsInfoList> _routerStatisticsInfoLists = new();

	static readonly RouteExceptionResponseInfoCollection _exceptionResponseInfes = new();

#pragma warning disable IDE0052 // 删除未读的私有成员
	static readonly LoopTask _ = new((cancellationToken) =>
#pragma warning restore IDE0052 // 删除未读的私有成员
	{
		var now = DateTime.Now;
		var collectBeginTime = now;
		var routeStatisticsInfoValidRouteTimeMin = now.AddSeconds(-1 * Config.Router.RouteStatisticsInfoRetainSeconds);

		////////////////////////////////////////////////
		// 1/7，检查更新当前全部终点状态：
		////////////////////////////////////////////////
		var routers = _routers;
		if (routers != null)
		{
			foreach (var router in routers)
			{
				var endPoints = router.RouteRule?.EndPoints;
				if (endPoints?.Length > 0)
				{
					var isEndPointsInRouteRateControlling = false;
					var isEndPointsInStateChanging = false;
					foreach (var endPoint in endPoints)
					{
						if (endPoint.IsInRouteRateControlling(now) == true)
						{
							// !!!
							isEndPointsInRouteRateControlling =
								true;
							// !!!
						}
						if (endPoint.State == EndPointState.Enabling
							|| endPoint.State == EndPointState.Disabling)
						{
							isEndPointsInStateChanging = true;
						}
					}
					if (isEndPointsInRouteRateControlling == false && isEndPointsInStateChanging)
					{
						foreach (var endPoint in endPoints)
						{
							if (endPoint.State == EndPointState.Enabling)
							{
								endPoint.State =
									EndPointState.Enable;
							}
							else if (endPoint.State == EndPointState.Disabling)
							{
								endPoint.State =
									EndPointState.Disable;
							}
						}
					}
				}
			}
		}

		////////////////////////////////////////////////
		// 2/7，获取需要被收集，聚合的统计信息记录器：
		////////////////////////////////////////////////
		var recordersJustCreated = new List<RouteStatisticsInfoRecorder>();
		while (cancellationToken.IsCancellationRequested == false
			&& _routeStatisticsInfoRecorderCollectQueue.TryDequeue(out var routeStatisticsInfoRecorder))
		{
			// 异常情况”RouterName.Length < 1“：
			if (routeStatisticsInfoRecorder.RouterName?.Length > 0
			// 客户端中断连接时，会产生”EndPointName.Length < 1“：
			&& routeStatisticsInfoRecorder.EndPointName?.Length > 0)
			{
				// !!!
				recordersJustCreated.Add(routeStatisticsInfoRecorder);
				// 复用已完成的记录器，异常请求只在后台整理。
				_exceptionResponseInfes.Collect(routeStatisticsInfoRecorder, routeStatisticsInfoValidRouteTimeMin);
				// !!!
			}
		}

		////////////////////////////////////////////////
		// 3/7，根据响应时间排序终点请求Url：
		////////////////////////////////////////////////
		if (recordersJustCreated?.Count > 0)
		{
			List<RouteStatisticsInfoRecorder> responseDurationRankList = [];

			// 1/4，根据响应时长，排序新产生记录器：
			var responseDurationRankListExisted = _responseDurationRankList;
			if (responseDurationRankListExisted?.Length > 0)
			{
				responseDurationRankList.AddRange(responseDurationRankListExisted);
			}
			responseDurationRankList.AddRange(recordersJustCreated);

			responseDurationRankList.Sort((recorderA, recorderB) =>
			{
				var recorderAMillisecondsToResponse =
					recorderA.MillisecondsToResponse;
				var recorderBMillisecondsToResponse =
					recorderB.MillisecondsToResponse;
				if (recorderAMillisecondsToResponse < recorderBMillisecondsToResponse)
				{
					return 1;
				}
				else if (recorderAMillisecondsToResponse > recorderBMillisecondsToResponse)
				{
					return -1;
				}
				return 0;
			});

			// 2/4，过滤要屏蔽的终点请求Url：
			var rankListExceptedUrlKeys = Config.Router.ResponseDurationRankListExceptedUrlKeys;
			for (var recorderIndex = responseDurationRankList.Count - 1; recorderIndex >= 0; recorderIndex--)
			{
				var recorder = responseDurationRankList[recorderIndex];
				var isRecorderInvalid = false;
				if (recorder.Type != RouteStatisticsInfoRecorder.RecorderType.ResponseRecorder)
				{
					// !!!
					isRecorderInvalid = true;
					// !!!
				}
				else if (rankListExceptedUrlKeys?.Length > 0)
				{
					var recorderEndPointRequestUrl = recorder.EndPointRequestUrl;
					foreach (var rankListExceptedUrlKey in rankListExceptedUrlKeys)
					{
						if (rankListExceptedUrlKey?.Length > 0
						&& recorderEndPointRequestUrl?.Contains(rankListExceptedUrlKey, StringComparison.OrdinalIgnoreCase) == true)
						{
							// !!!
							isRecorderInvalid = true;
							break;
							// !!!
						}
					}
				}
				if (isRecorderInvalid)
				{
					responseDurationRankList.RemoveAt(recorderIndex);
				}
			}

			// 3/4，只保留指定数量的请求信息：
			var responseDurationRankListLengthInConfig = Config.Router.ResponseDurationRankListLength;
			if (responseDurationRankListLengthInConfig < 0)
			{
				responseDurationRankListLengthInConfig =
					RouterConfig.ResponseDurationRankListLengthDefault;
			}
			if (responseDurationRankList.Count > responseDurationRankListLengthInConfig)
			{
				responseDurationRankList.RemoveRange(
					responseDurationRankListLengthInConfig,
					responseDurationRankList.Count
						- responseDurationRankListLengthInConfig
				);
			}

			////////////////////////////////////////////////
			// !!!
			_responseDurationRankList = [.. responseDurationRankList];
			// !!!

			// 4/4，检查是否存在超出最大响应时间的请求，并通知管理者：
			var routerConfig = Config.Router;
			if (_lastTimeOfSendMailToWarningResponseDurationOverMax.AddSeconds(
				routerConfig.SMTPServerSendMailIntervalSeconds) <= now)
			{
				var smtpServerAddress = routerConfig.SMTPServerAddress;
				var smtpServerPort = routerConfig.SMTPServerPort;
				var isSMTPServerSSLEnable = routerConfig.IsSMTPServerSSLEnable;
				var smtpServerAccount = routerConfig.SMTPServerAccount;
				var smtpServerPassword = routerConfig.SMTPServerPassword;
				var smtpAuthorName = routerConfig.SMTPAuthorName;
				if (smtpAuthorName == null || smtpAuthorName.Length < 1)
				{
					smtpAuthorName = smtpServerAccount;
				}
				if (smtpServerAddress?.Length > 0
				&& smtpServerAccount?.Length > 0
				&& smtpServerPassword?.Length > 0)
				{
					var responseDurationMillisecondsMax =
						routerConfig.ResponseDurationMillisecondsMaxToWarningManager;
					if (responseDurationMillisecondsMax > 0)
					{
						List<string>? endPointRequestUrlsWithResponseDurationOverMax = null;
						foreach (var recorder in responseDurationRankList)
						{
							if (
								recorder.MillisecondsToResponse
									> responseDurationMillisecondsMax
								&& recorder.IsResponseDurationChecked
									== false
							)
							{
								// !!!
								recorder.IsResponseDurationChecked =
									true;
								// !!!

								endPointRequestUrlsWithResponseDurationOverMax ??=
								[];
								// !!!
								endPointRequestUrlsWithResponseDurationOverMax.AddUnique(
									recorder.EndPointRequestUrl
								);
								// !!!
							}
						}

						if (endPointRequestUrlsWithResponseDurationOverMax?.Count > 0)
						{
							var warningMessageTitle = Config.Router.ResponseDurationMaxToWarningManagerMessageTitle;
							if (warningMessageTitle == null || warningMessageTitle.Length < 1)
							{
								warningMessageTitle =
									RouterConfig.ResponseDurationMaxToWarningManagerMessageTitleDefault;
							}
							var warningMessageFormatter = Config
								.Router
								.ResponseDurationMaxToWarningManagerMessageFormatter;
							if (
								warningMessageFormatter
									== null
								|| warningMessageFormatter.Length
									< 1
							)
							{
								warningMessageFormatter =
									RouterConfig.ResponseDurationMaxToWarningManagerMessageFormatterDefault;
							}

							var maxResponseDurationText = (responseDurationMillisecondsMax / 1000).ToString("F3") + "秒";
							var endPointRequestUrlsWithResponseDurationOverMaxText =
								StringUtil.StringWithStrings(
									endPointRequestUrlsWithResponseDurationOverMax,
									"\r\n",
									true
								);
							var warningMessage = warningMessageFormatter
									.Replace("#最大响应时间#", maxResponseDurationText)
									.Replace("#超时请求Urls#", endPointRequestUrlsWithResponseDurationOverMaxText);

							List<string>? managerEMails = null;
							var managers = routerConfig.ServiceAdministratorConfig?.Administrators;
							if (managers?.Length > 0)
							{
								foreach (var manager in managers)
								{
									if (manager.EMail?.Length > 0)
									{
										managerEMails ??= [];
										managerEMails.Add(manager.EMail);
									}
								}
							}

							////////////////////////////////////////////////
							////////////////////////////////////////////////
							////////////////////////////////////////////////
							if (managerEMails != null)
							{
								EMail.SendMailTo(
									smtpServerAddress,
									smtpServerPort,
									isSMTPServerSSLEnable,
									managerEMails,
									null,
									warningMessageTitle,
									warningMessage,
									null,
									smtpAuthorName,
									smtpServerAccount,
									smtpServerPassword
								);
								// !!!
								_lastTimeOfSendMailToWarningResponseDurationOverMax = now;
								// !!!
							}
							////////////////////////////////////////////////
							////////////////////////////////////////////////
							////////////////////////////////////////////////
						}
					}
				}
			}
		}

		////////////////////////////////////////////////
		// 4/7，聚合统计信息，路由名称维度：
		////////////////////////////////////////////////
		var recordersNeedPolymerized = recordersJustCreated;
		var rpsPeakDefinition = Config.Router.RouteStatisticsInfoRPSPeakDefinition;
		if (recordersNeedPolymerized?.Count > 0)
		{
			////////////////////////////////////////////////
			// 1/2，聚合当前路由统计信息：
			////////////////////////////////////////////////
			var statisticsInfoListsGroupBy_RouterName = recordersNeedPolymerized.ToRouteStatisticsInfoLists((recorder) =>
			{
				return RouteStatisticsInfoRecorder.CreateRouteStatisticsInfoNameWithRouterName(recorder.RouterName, null);
			},
			(recorderA, recorderB) =>
			{
				if (recorderA.RouterName.EqualsIgnoreCase(recorderB.RouterName))
				{
					return true;
				}
				return false;
			});

			////////////////////////////////////////////////
			// 2/2，更新当前路由统计信息：
			////////////////////////////////////////////////
			if (statisticsInfoListsGroupBy_RouterName?.Count > 0)
			{
				foreach (var statisticsInfoList in statisticsInfoListsGroupBy_RouterName)
				{
					if (statisticsInfoList.Count < 1)
					{
						continue;
					}

					var statisticsInfo = statisticsInfoList[0];
					var statisticsInfoRouterName =
						statisticsInfo.RouterName ?? string.Empty;

					////////////////////////////////////////////////
					// 1/2，更新当前路由统计信息：
					////////////////////////////////////////////////
					var routerStatisticsInfoList = _routerStatisticsInfoLists.GetOrAdd(statisticsInfoRouterName, []);
					lock (routerStatisticsInfoList)
					{
						routerStatisticsInfoList.MergeFrom(statisticsInfoList, routeStatisticsInfoValidRouteTimeMin, (statisticsInfo) =>
						{
							// !!! 当前峰值日志记录 !!!
							if (rpsPeakDefinition > 0 && statisticsInfo.RequestsCount >= rpsPeakDefinition)
							{
								Log.RPSPeak.Logs(statisticsInfo.Name ?? nameof(Router), statisticsInfo.ToString(), null);
							}
						});
						// !!!
						routerStatisticsInfoList.SortWithRouteTimeASC();
						// !!!
					}

					////////////////////////////////////////////////
					// 2/2，更新整体路由统计信息：
					////////////////////////////////////////////////
					var allRouterStatisticsInfoList = _routerStatisticsInfoLists.GetOrAdd(Router.RouterName_All, []);
					lock (allRouterStatisticsInfoList)
					{
						allRouterStatisticsInfoList.MergeFrom(statisticsInfoList, routeStatisticsInfoValidRouteTimeMin, (statisticsInfo) =>
						{
							// !!! 当前峰值日志记录 !!!
							if (rpsPeakDefinition > 0
								&& statisticsInfo.RequestsCount >= rpsPeakDefinition)
							{
								Log.RPSPeak.Logs(statisticsInfo.Name ?? nameof(Router), statisticsInfo.ToString(), null);
							}
						});
						// !!!
						allRouterStatisticsInfoList.SortWithRouteTimeASC();
						// !!!
					}
				}
			}
		}

		////////////////////////////////////////////////
		// 5/7，整体清理过期无效的统计信息：
		////////////////////////////////////////////////
		// 即使没有新请求，也要按相同保留时间清理异常请求。
		_exceptionResponseInfes.RemoveExpired(routeStatisticsInfoValidRouteTimeMin);
		foreach (var routerStatisticsInfoListInfo in _routerStatisticsInfoLists)
		{
			var routerStatisticsInfoList = routerStatisticsInfoListInfo.Value;
			if (routerStatisticsInfoList == null)
			{
				continue;
			}
			lock (routerStatisticsInfoList)
			{
				// 1/2，清理整体统计信息：
				for (var routerStatisticsInfoIndex = routerStatisticsInfoList.Count - 1;
					routerStatisticsInfoIndex >= 0;
					routerStatisticsInfoIndex--
				)
				{
					var routerStatisticsInfo = routerStatisticsInfoList[
						routerStatisticsInfoIndex
					];
					if (routerStatisticsInfo.RouteTimeInSecond <= routeStatisticsInfoValidRouteTimeMin)
					{
						// !!!
						routerStatisticsInfoList.RemoveAt(routerStatisticsInfoIndex);
						// !!!
					}
				}
				// 2/2，清理终点的统计信息：
				var endPointRouteStatisticsInfesList = routerStatisticsInfoList.EndPointRouteStatisticsInfes;
				if (endPointRouteStatisticsInfesList != null && endPointRouteStatisticsInfesList.IsEmpty != true)
				{
					foreach (var endPointRouteStatisticsInfoKeyValue in endPointRouteStatisticsInfesList)
					{
						var endPointRouteStatisticsInfes = endPointRouteStatisticsInfoKeyValue.Value;
						if (endPointRouteStatisticsInfes?.Count > 0)
						{
							for (var endPointRouteStatisticsInfoIndex = endPointRouteStatisticsInfes.Count - 1;
								endPointRouteStatisticsInfoIndex >= 0;
								endPointRouteStatisticsInfoIndex--)
							{
								var endPointRouteStatisticsInfo =
									endPointRouteStatisticsInfes[
										endPointRouteStatisticsInfoIndex
									];
								if (
									endPointRouteStatisticsInfo.RouteTimeInSecond
									<= routeStatisticsInfoValidRouteTimeMin
								)
								{
									// !!!
									endPointRouteStatisticsInfes.RemoveAt(
										endPointRouteStatisticsInfoIndex
									);
									// !!!
								}
							}
						}
					}
				}
			}
		}

		////////////////////////////////////////////////
		// 6/7，聚合统计信息，路由名称+路由终点维度：
		////////////////////////////////////////////////
		if (recordersNeedPolymerized?.Count > 0)
		{
			////////////////////////////////////////////////
			// 1/2，聚合当前【路由终点】统计信息：
			////////////////////////////////////////////////
			var statisticsInfoListsGroupBy_RouterNameAndEndPoint =
				recordersNeedPolymerized.ToRouteStatisticsInfoLists(
					(recorder) =>
					{
						return RouteStatisticsInfoRecorder.CreateRouteStatisticsInfoNameWithRouterName(
							recorder.RouterName,
							recorder.EndPointName
						);
					},
					(recorderA, recorderB) =>
					{
						var isSameRouter =
							recorderA.RouterName?.EqualsIgnoreCase(
								recorderB.RouterName
							) == true;
						if (isSameRouter)
						{
							var isSameEndPoint =
								recorderA.EndPointName?.EqualsIgnoreCase(
									recorderB.EndPointName
								) == true;
							if (isSameEndPoint)
							{
								return true;
							}
						}
						return false;
					}
				);

			////////////////////////////////////////////////
			// 2/2，更新当前【路由终点】统计信息：
			////////////////////////////////////////////////
			if (statisticsInfoListsGroupBy_RouterNameAndEndPoint?.Count > 0)
			{
				foreach (
					var routerEndPointstatisticsInfoList in statisticsInfoListsGroupBy_RouterNameAndEndPoint
				)
				{
					if (routerEndPointstatisticsInfoList.Count < 1)
					{
						continue;
					}

					var statisticsInfo =
						routerEndPointstatisticsInfoList[0];
					string statisticsInfoRouterName =
						statisticsInfo.RouterName ?? string.Empty;
					string statisticsInfoEndPointName =
						statisticsInfo.EndPointName ?? string.Empty;

					////////////////////////////////////////////////
					// 1/2，更新当前路由【终点】统计信息：
					////////////////////////////////////////////////
					var routerStatisticsInfoList =
						_routerStatisticsInfoLists.GetOrAdd(
							statisticsInfoRouterName,
							[]
						);
					lock (routerStatisticsInfoList)
					{
						routerStatisticsInfoList.EndPointRouteStatisticsInfes ??=
							new();
						var endPointRouteStatisticsInfes =
							routerStatisticsInfoList.EndPointRouteStatisticsInfes;

						var endPointStatisticsInfoList =
							endPointRouteStatisticsInfes.GetOrAdd(
								statisticsInfoEndPointName,
								new RouterStatisticsInfoList()
							);

						var endPointRouteFailedsCount = 0;
						endPointStatisticsInfoList.MergeFrom(
							routerEndPointstatisticsInfoList,
							routeStatisticsInfoValidRouteTimeMin,
							(statisticsInfo) =>
							{
								endPointRouteFailedsCount +=
									statisticsInfo.RequestsCountFailed;

								// !!! 当前峰值日志记录 !!!
								if (
									rpsPeakDefinition
										> 0
									&& statisticsInfo.RequestsCount
										>= rpsPeakDefinition
								)
								{
									Log.RPSPeak.Logs(
										statisticsInfo.Name ?? nameof(Router),
										statisticsInfo.ToString(),
										null
									);
								}
							}
						);
						// !!!
						endPointStatisticsInfoList.SortWithRouteTimeASC();
						// !!!

						////////////////////////////////////////////////
						// !!! 根据最新统计信息，更新终点状态 !!!
						////////////////////////////////////////////////
						if (
							endPointStatisticsInfoList.Count > 0
							&& endPointRouteFailedsCount > 0
						)
						{
							var endPoint =
								Router.GetEndPointWithRouterName(
									statisticsInfoRouterName,
									statisticsInfoEndPointName
								);
							if (
								endPoint != null
								&& endPoint.IsDisableByExceptionRateOverMax
									== false
							)
							{
								var routerConfig =
									Config.Router;
								var endPointExceptionRateWatchingSeconds =
									endPoint.ExceptionRateWatchingSeconds
									>= 0
										? endPoint.ExceptionRateWatchingSeconds
										: routerConfig.EndPointExceptionRateWatchingSeconds;
								var endPointExceptionRateMax =
									endPoint.ExceptionRateMax
									>= 0
										? endPoint.ExceptionRateMax
										: routerConfig.EndPointExceptionRateMax;
								if (
									endPointExceptionRateWatchingSeconds
										> 0
									&& endPointExceptionRateMax
										> 0
								)
								{
									// !!!
									var endPointWatchingTimeEnd =
										endPointStatisticsInfoList[
											^1
										].RouteTimeInSecond;
									var endPointWatchingTimeBegin =
										endPointWatchingTimeEnd.AddSeconds(
											-1
												* endPointExceptionRateWatchingSeconds
										);
									// !!!
									var endPointRequestsCountToEndPoint =
										0;
									var endPointRequestsCountToEndPointFailed =
										0;

									foreach (var endPointStatisticsInfo in endPointStatisticsInfoList)
									{
										if (endPointStatisticsInfo.RouteTimeInSecond >= endPointWatchingTimeBegin)
										{
											endPointRequestsCountToEndPoint +=
												endPointStatisticsInfo.RequestsCountToEndPoint;
											endPointRequestsCountToEndPointFailed +=
												endPointStatisticsInfo.RequestsCountFailed;
										}
									}

									var endPointExceptionRateWatchingRequestsCountMin =
										endPoint.ExceptionRateWatchingRequestsCountMin
										>= 0
											? endPoint.ExceptionRateWatchingRequestsCountMin
											: routerConfig.EndPointExceptionRateWatchingRequestsCountMin;
									if (
										endPointRequestsCountToEndPoint
										>= endPointExceptionRateWatchingRequestsCountMin
									)
									{
										var endPointExceptionRate =
											(double)endPointRequestsCountToEndPointFailed
											/ (double)endPointRequestsCountToEndPoint;
										////////////////////////////////////////////////
										// !!!⚠
										if (
											endPointExceptionRate
											> endPointExceptionRateMax
										)
										{
											endPoint.IsDisableByExceptionRateOverMax =
												true;
											endPoint.ExceptionRateOverMaxTime =
												now;
										}
										// !!!⚠
										////////////////////////////////////////////////
									}
								}
							}
						}
					}

					////////////////////////////////////////////////
					// 2/2，更新整体路由【终点】统计信息：
					////////////////////////////////////////////////
					var allRouterStatisticsInfoList =
						_routerStatisticsInfoLists.GetOrAdd(
							Router.RouterName_All,
							[]
						);
					lock (allRouterStatisticsInfoList)
					{
						allRouterStatisticsInfoList.EndPointRouteStatisticsInfes ??=
							new();
						var endPointRouteStatisticsInfes =
							allRouterStatisticsInfoList.EndPointRouteStatisticsInfes;

						var endPointStatisticsInfoList =
							endPointRouteStatisticsInfes.GetOrAdd(
								statisticsInfoEndPointName,
								new RouterStatisticsInfoList()
							);
						endPointStatisticsInfoList.MergeFrom(
							routerEndPointstatisticsInfoList,
							routeStatisticsInfoValidRouteTimeMin,
							(statisticsInfo) =>
							{
								// !!! 当前峰值日志记录 !!!
								if (
									rpsPeakDefinition
										> 0
									&& statisticsInfo.RequestsCount
										>= rpsPeakDefinition
								)
								{
									Log.RPSPeak.Logs(
										statisticsInfo.Name ?? nameof(Router),
										statisticsInfo.ToString(),
										null);
								}
							}
						);
						// !!!
						endPointStatisticsInfoList.SortWithRouteTimeASC();
						// !!!
					}
				}
			}
		}

		////////////////////////////////////////////////
		// 7/7，尝试重新启用之前禁用的终点：
		////////////////////////////////////////////////
		var allEndPoints = Router.GetAllEndPoints();
		if (allEndPoints?.Count > 0)
		{
			foreach (var endPoint in allEndPoints)
			{
				if (endPoint.IsDisableByExceptionRateOverMax == true)
				{
					if (endPoint.ExceptionRetryTime < now)
					{
						////////////////////////////////////////////////
						// !!!⚠
						{
							endPoint.IsDisableByExceptionRateOverMax =
								false;
						}
						// !!!⚠
						////////////////////////////////////////////////
					}
				}
			}
		}

		// !!! 更新统计信息收集任务执行时间 !!!
		//DateTime collectEndTime = DateTime.Now;
		//{
		//        ////////////////////////////////////////////////
		//        _routeStatisticsInfoRecorderLastCollectBeginTime = collectBeginTime;
		//        _routeStatisticsInfoRecorderLastCollectEndTime = collectEndTime;
		//}

		return true;
	}, 0.5F);

	////////////////////////////////////////////////
	// @类方法
	////////////////////////////////////////////////

	#region 类方法

	public static void UpateRouters(ConfigFiles.RouterConfig routerConfig)
	{
		List<Router> routers = [];
		if (routerConfig?.RouteRules?.Length > 0)
		{
			var routeRules = routerConfig.RouteRules;
			foreach (var routeRule in routeRules.OrderBy(routeRule => routeRule.OrderIndex))
			{
				var router = new Router(routeRule);
				{ }
				routers.Add(router);
			}
		}
		_routers = routers?.ToArray();
	}

	//public static bool IsAccessControllAllowOrigin(string? domain)
	//{
	//        if (domain == null
	//                || domain.Length < 1)
	//        {
	//                // !!!⚠ 没有明确的来源信息时，默认允许请求 ⚠!!!
	//                return true;
	//        }

	//        if (Config.Router.IsAnyDomainAccessControllAllow)
	//        {
	//                return true;
	//        }

	//        var domainHost = domain.SubstringBetween("://", "/");
	//        domainHost ??= domain;
	//        domainHost = domainHost.SubstringBetween(null, ":");

	//        if (domainHost?.Length > 0)
	//        {
	//                var credibleDomains = Config.Router.Access_Control_Allow_Origins;
	//                if (credibleDomains?.Length > 0)
	//                {
	//                        foreach (var credibleDomain
	//                            in
	//                            credibleDomains)
	//                        {
	//                                if (domainHost.EndsWith(
	//                                        credibleDomain,
	//                                        StringComparison.OrdinalIgnoreCase)
	//                                        == true)
	//                                {
	//                                        return true;
	//                                }
	//                        }
	//                }
	//        }
	//        return false;
	//}

	public static Task<bool> RouteInContext(HttpContext httpContext)
	{
		return RouteInContext(httpContext, RunTime.RouteRequestPaths);
	}

	public static async Task<bool> RouteInContext(HttpContext httpContext, RouteRequestPathOptions options)
	{
		if (httpContext == null)
		{
			return false;
		}

		// #路由统计#
		var isRouteFinished = false;
		var responseStatusCodeSpecified = 0;
		string? responseStatusDescriptionSpecified = null;
		var isRouteCompleted = false;
		Exception? routeException = null;

		var request = httpContext.Request;
		var connectionIpEndPoints = request.GetConnectionIpEndPoints();
		if (connectionIpEndPoints == null)
		{
			isRouteFinished = true;
			responseStatusCodeSpecified = (int)HttpStatusCode.BadRequest;
			responseStatusDescriptionSpecified = "无法生成客户端连接Ip终结点集合对象。";
		}
		else if (connectionIpEndPoints.KeyIpEndPointAddress is not { } clientKeyIpAddress)
		{
			isRouteFinished = true;
			responseStatusCodeSpecified = (int)HttpStatusCode.BadRequest;
			responseStatusDescriptionSpecified = "无法获取客户端连接的关键Ip地址。";
		}
		else if (Config.BlockRecord.IsIpBlocked(connectionIpEndPoints.KeyIpEndPointAddress, DateTimeOffset.Now))
		{
			isRouteFinished = true;
			responseStatusCodeSpecified = (int)HttpStatusCode.Forbidden;
			responseStatusDescriptionSpecified = "非法的客户端Ip地址。";
		}
		var requestHeaders = request.Headers;
		var response = httpContext.Response;
		var responseHeaders = response.Headers;


		////////////////////////////////////////////////
		// 1/2，使用路由器路由请求：
		////////////////////////////////////////////////
		RouteStatisticsInfoRecorder? routeStatisticsInfoRecorderForResponse = null;
		try
		{
			if (isRouteFinished != true)
			{
				using var clientRequestPathInfo = request.CreateClientRequestPathInfo(options)
					?? throw new ArgumentException("无法创建客户端请求路径信息。", request.Path.ToString());
				var routers = _routers;

				routeStatisticsInfoRecorderForResponse = new RouteStatisticsInfoRecorder(
					RouteStatisticsInfoRecorder.RecorderType.ResponseRecorder);
				var routeTime = routeStatisticsInfoRecorderForResponse.RouteTime;

				Router? routerMatched = null;
				if (routers != null)
				{
					foreach (var router in routers)
					{
						if (router.IsEnableToMatchedClientRequestPathInfo(clientRequestPathInfo, routeTime) == true)
						{
							// !!!
							routerMatched = router;
							// !!!☆ #路由统计# ☆
							routeStatisticsInfoRecorderForResponse.EndMatchRouteRule();
							// !!!☆ #路由统计# ☆
							break;
						}
					}
				}

				if (routerMatched != null && routerMatched.RouteRule.IsClientRequestAllowed(connectionIpEndPoints, request.Path.Value))
				{
					var clientRequestInfo = await routerMatched.CreateClientRequestInfoAsync(request, clientRequestPathInfo);
					// !!!☆ #路由统计# ☆
					routeStatisticsInfoRecorderForResponse.EndCreateRequestHashCode();
					// !!!☆ #路由统计# ☆

					var namesOfEndPointExcepted = new List<string>();
					{
						clientRequestPathInfo.NamesOfEndPointExcepted =
							namesOfEndPointExcepted;
					}

					EndPointRequestInfo? endPointRequestInfo = null;
					do
					{
						try
						{
							endPointRequestInfo = routerMatched.CreateEndPointRequestInfo(
								request, clientRequestPathInfo, clientRequestInfo, routeTime);
							////////////////////////////////////////////////
							var routeStatisticsInfoRecorderForRequest = new RouteStatisticsInfoRecorder(
								RouteStatisticsInfoRecorder.RecorderType.RequestRecorder);
							{
								routeStatisticsInfoRecorderForRequest.EndRequest(
									routerMatched,
									endPointRequestInfo
								);
							}
							// !!!☆ #路由统计# ☆
							_routeStatisticsInfoRecorderCollectQueue.Enqueue(
								routeStatisticsInfoRecorderForRequest
							);
							// !!!☆ #路由统计# ☆
							////////////////////////////////////////////////
							if (endPointRequestInfo?.IsValidToRequestToEndPoint == true
								&& endPointRequestInfo.EndPoint != null)
							{
								// !!!
								isRouteCompleted = false;
								routeException = null;
								// !!!

								////////////////////////////////////////////////
								////////////////////////////////////////////////
								////////////////////////////////////////////////
								// !!!⚠
								await routerMatched.RouteRequest(request, routeTime, connectionIpEndPoints, httpContext,
									clientRequestPathInfo, routerMatched.RouteRule,
									endPointRequestInfo.EndPoint, endPointRequestInfo, response,
									routeStatisticsInfoRecorderForResponse);
								// !!! ⚠⚠⚠⚠⚠⚠⚠⚠ !!! //
								{
									isRouteCompleted = true;
								}
								// !!! ⚠⚠⚠⚠⚠⚠⚠⚠ !!! //
								////////////////////////////////////////////////
								////////////////////////////////////////////////
								////////////////////////////////////////////////
							}
						}
						catch (Exception exception)
						{
							var endPointName = endPointRequestInfo?.EndPointName;
							Log.Exception.Logs(nameof(Router), "路由请求，终点路由失败，程序异常，终点：" + endPointName + "。",
								exception);
							// !!!
							isRouteCompleted = false;
							routeException = exception;
							if (endPointName?.Length > 0)
							{
								namesOfEndPointExcepted.AddUnique(
									endPointName
								);
							}
							// !!!
						}
						finally
						{
							////////////////////////////////////////////////
							// 3/4，确认最终路由状态：
							////////////////////////////////////////////////
							// !!!⚠ 当前没有可用的终点时，结束本次路由⚠!!!
							if ( // 无法生成有效的终点请求信息：
								endPointRequestInfo == null
								// 手动指定的终点，不进行切换：
								|| routerMatched.RouteRule?.EndPointRoutePolicy == EndPointRoutePolicy.Manual
								// 当前没有发生异常：
								|| routeException == null
								// 已有可用的响应（已写入）：
								|| response.HasStarted == true)
							{
								// !!!
								isRouteFinished = true;
								// !!!
							}
							// !!!⚠ ; ⚠!!!
							else
							{
								// !!!⚠ 当本次路由发生了【非线路异常】时（非超时，非TCP错误），即路由器本身出现了故障，结束本次路由⚠!!!
								var isRouteExceptionNotTimeoutException = (routeException is TaskCanceledException) == false;
								var isRouteExceptionNotTimeoutException2 = (routeException is TimeoutException) == false;
								var isRouteExceptionNotTCPException
									= (routeException.InnerException is System.Net.Sockets.SocketException) == false;
								if (isRouteExceptionNotTimeoutException && isRouteExceptionNotTimeoutException2
									&& isRouteExceptionNotTCPException)
								{
									// !!!
									isRouteFinished = true;
									// !!!
								}
							}

							////////////////////////////////////////////////
							// 3/4，收集&记录路由统计信息：
							////////////////////////////////////////////////
							{
								var routeStatisticsInfoRecorderForResponseWillCollected = routeStatisticsInfoRecorderForResponse;
								if (isRouteFinished == false)
								{
									routeStatisticsInfoRecorderForResponseWillCollected
										= new RouteStatisticsInfoRecorder(routeStatisticsInfoRecorderForResponse);
									// !!!
									routeStatisticsInfoRecorderForResponse.RestartWatchForCurrentStatisticsSection();
									// !!!
								}

								var responseStatusCode = response.StatusCode;
								if (routeException is HttpRequestException httpRequestException
									&& httpRequestException.StatusCode is { } httpRequestExceptionStatusCode)
								{
									responseStatusCode = (int)httpRequestExceptionStatusCode;
								}
								// !!!
								routeStatisticsInfoRecorderForResponseWillCollected.EndResponse(
									routerMatched, endPointRequestInfo, isRouteCompleted == false, responseStatusCode);
								// !!!
								_routeStatisticsInfoRecorderCollectQueue.Enqueue(routeStatisticsInfoRecorderForResponseWillCollected);
								// !!!
							}
						}
					} while (isRouteFinished == false);
				}
				else if (routerMatched != null)
				{
					response.StatusCode = (int)System.Net.HttpStatusCode.TooManyRequests;
					response.ContentType = HttpHeaderValues.Content_Types.Text_Plain_Utf8;
					await response.Body.WriteAsync(ResponseBodies.Http_429_ClientRequestRateLimitExceeded.ToUtf8Bytes());
					isRouteCompleted = true;
				}
			}
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(nameof(Router), "路由请求，全部终点路由失败，程序异常。", exception);
			//
			//isRouteFinished = true;
			isRouteCompleted = false;
			routeException = exception;
		}

		try
		{
			////////////////////////////////////////////////
			// 4/5，【最终】，没有进行路由的请求，如果不符合当前控制器的路径规则，
			// 则抛出Http状态异常：
			////////////////////////////////////////////////
			if (isRouteCompleted != true && response.HasStarted != true)
			{
				// !!!
				int responseStatusCode = (int)System.Net.HttpStatusCode.NotFound;
				if (responseStatusCodeSpecified != 0)
				{
					responseStatusCode = responseStatusCodeSpecified;
				}
				string responseContentType = HttpHeaderValues.Content_Types.Text_Plain_Utf8;
				string responseContent = ResponseBodies.Http_404_NoneRouteRuleMatched;
				if (responseStatusDescriptionSpecified?.Length > 0)
				{
					responseContent = responseStatusDescriptionSpecified;
				}
				if (routeException != null)
				{
					responseStatusCode = routeException is HttpRequestException
					{
						StatusCode: HttpStatusCode.BadGateway
					}
					? (int)HttpStatusCode.BadGateway
					: (int)HttpStatusCode.InternalServerError;
					responseContentType = HttpHeaderValues.Content_Types.Text_Plain_Utf8;
					responseContent = ResponseBodies.Http_500_RouteFailedWithProgramError;
					responseContent += "\r\n" + routeException.ToString();
				}
				response.StatusCode = responseStatusCode;
				response.ContentType = responseContentType;
				if (responseContent != null)
				{
					await response.Body.WriteAsync(responseContent.ToUtf8Bytes());
				}
			}

			////////////////////////////////////////////////
			// 5/5，【最终】，记录Http请求日志：
			////////////////////////////////////////////////
			if (Config.Router.IsHttpRequestLogEnable)
			{
				Router.DidLogHttpRequest(request, connectionIpEndPoints, response, routeStatisticsInfoRecorderForResponse);
			}
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(
				nameof(Router),
				"路由请求【最终阶段】失败，程序异常。",
				exception
			);
		}

		return isRouteCompleted;
	}

	public static List<string>? GetAllRouterNames()
	{
		var routers = _routers;
		if (routers == null || routers.Length < 1)
		{
			return null;
		}

		var allRouterNames = new List<string>();
		foreach (var router in routers)
		{
			if (router.Name?.Length > 0)
			{
				allRouterNames.Add(router.Name);
			}
		}
		return allRouterNames;
	}

	public static Router? GetRouterWithRouterName(string routerName)
	{
		var routers = _routers;
		if (routers?.Length > 0)
		{
			foreach (var router in routers)
			{
				if (routerName.EqualsIgnoreCase(router.Name) == true)
				{
					return router;
				}
			}
		}
		return null;
	}

	public static RouteRule? GetRouteRuleWithRouterName(string routerName)
	{
		var routers = _routers;
		if (routers?.Length > 0)
		{
			foreach (var router in routers)
			{
				if (routerName.EqualsIgnoreCase(router.Name) == true)
				{
					return router.RouteRule;
				}
			}
		}
		return null;
	}

	public static List<EndPoint>? GetAllEndPoints()
	{
		var routers = _routers;
		if (routers == null || routers.Length < 1)
		{
			return null;
		}

		var allEndPoints = new List<EndPoint>();
		foreach (var router in routers)
		{
			var routerEndPoints = router.RouteRule?.EndPoints;
			if (routerEndPoints?.Length > 0)
			{
				allEndPoints.AddRange(routerEndPoints);
			}
		}
		return allEndPoints;
	}

	public static EndPoint? GetEndPointWithRouterName(string routerName, string endPointName)
	{
		if (endPointName == null || endPointName.Length < 1)
		{
			return null;
		}
		var router = Router.GetRouteRuleWithRouterName(routerName);
		if (router == null)
		{
			return null;
		}

		var endPoint = router.GetEndPointWithName(endPointName);
		{ }
		return endPoint;
	}

	public static RouteRule? GetRouterRuleWithClientRequestPathInfo(
		ClientRequestPathInfo clientRequestPathInfo,
		DateTime routeTime
	)
	{
		var routers = _routers;
		if (routers?.Length > 0)
		{
			foreach (var router in routers)
			{
				if (
					router.RouteRule?.IsEnableToMatchedClientRequestPathInfo(
						clientRequestPathInfo,
						routeTime
					) == true
				)
				{
					return router.RouteRule;
				}
			}
		}
		return null;
	}

	public static RouteStatisticsInfoRecorder[]? ResponseDurationRankList => _responseDurationRankList;

	public static RouterStateStatisticsInfoInSecond? GetRouterStateStatisticsInfoInSecond(
		string routerName, DateTime secondTime, bool isGetRouterRouteStateStatisticsInfoInLastSecond
	)
	{
		if (routerName.Length < 1)
		{
			return null;
		}

		var routerStateStatisticsInfoInSecond = new RouterStateStatisticsInfoInSecond();

		////////////////////////////////////////////////
		// 1/，【路由、终点】的【状态】信息：
		////////////////////////////////////////////////
		{
			var endPointStateInfes = new Dictionary<string, EndPointStateInfo>();
			{
				ICollection<EndPoint>? allEndPoints;
				var router = Router.GetRouteRuleWithRouterName(routerName);
				if (router != null)
				{
					// !!! 修正”routerName‘：
					if (router.Name != null)
					{
						routerName = router.Name;
					}
					// !!!
					allEndPoints = router.EndPoints;
				}
				else
				{
					allEndPoints = Router.GetAllEndPoints();
				}
				if (allEndPoints?.Count > 0)
				{
					foreach (var endPoint in allEndPoints)
					{
						endPointStateInfes.TryAdd(
							endPoint.Name,
							new(endPoint)
						);
					}
				}
			}
			routerStateStatisticsInfoInSecond.EndPointStateInfes = endPointStateInfes;
		}

		////////////////////////////////////////////////
		// 2/，【路由、终点】的【流量】信息：
		////////////////////////////////////////////////
		var requestsCountFromEndPoints = 0;
		var requestsCountFailedFromEndPoints = 0;
		var requestsCountToEndPointFromEndPoints = 0;
		var endPointRouteStatisticsInfesInSecond =
			new Dictionary<string, RouteStatisticsInfo>();
		if (
			_routerStatisticsInfoLists.TryGetValue(
				routerName,
				out var routerRouteStatisticsInfoList
			)
		)
		{
			lock (routerRouteStatisticsInfoList)
			{
				for (
					var routerRouteStatisticsInfoIndex =
						routerRouteStatisticsInfoList.Count - 1;
					routerRouteStatisticsInfoIndex >= 0;
					routerRouteStatisticsInfoIndex--
				)
				{
					var routerRouteStatisticsInfo =
						routerRouteStatisticsInfoList[
							routerRouteStatisticsInfoIndex
						];
					if (
						secondTime.EqualsInSecond(
							routerRouteStatisticsInfo.RouteTimeInSecond
						) || isGetRouterRouteStateStatisticsInfoInLastSecond
					)
					{
						////////////////////////////////////////////////
						// 3/，当前【路由】的最后一秒的流量信息：
						////////////////////////////////////////////////
						// !!!
						routerStateStatisticsInfoInSecond.RouterRouteStatisticsInfo
						// ⚠注意这里要复制一份，避免数据污染。
						= new RouteStatisticsInfo(
							routerRouteStatisticsInfo
						);
						// !!!
						break;
					}
				}

				////////////////////////////////////////////////
				// 3/，当前【终点】的最后一秒的流量信息：
				////////////////////////////////////////////////
				var allEndPointRouteStatisticsInfes = routerRouteStatisticsInfoList
					.EndPointRouteStatisticsInfes
					?.Values;
				if (allEndPointRouteStatisticsInfes?.Count > 0)
				{
					foreach (
						var endPointRouteStatisticsInfoList in allEndPointRouteStatisticsInfes
					)
					{
						if (endPointRouteStatisticsInfoList?.Count > 0)
						{
							for (
								var endPointRouteStatisticsInfoIndex =
									endPointRouteStatisticsInfoList.Count
									- 1;
								endPointRouteStatisticsInfoIndex
									>= 0;
								endPointRouteStatisticsInfoIndex--
							)
							{
								var endPointRouteStatisticsInfo =
									endPointRouteStatisticsInfoList[
										endPointRouteStatisticsInfoIndex
									];
								if (
									secondTime.EqualsInSecond(
										endPointRouteStatisticsInfo.RouteTimeInSecond
									)
									|| isGetRouterRouteStateStatisticsInfoInLastSecond
								)
								{
									// ⚠注意这里要复制一份，避免数据污染。
									endPointRouteStatisticsInfo =
										new(
											endPointRouteStatisticsInfo
										);
									// !!!
									endPointRouteStatisticsInfesInSecond.Add(
										endPointRouteStatisticsInfo.EndPointName
											?? string.Empty,
										endPointRouteStatisticsInfo
									);
									// !!!
									requestsCountFromEndPoints +=
										endPointRouteStatisticsInfo.RequestsCount;
									requestsCountFailedFromEndPoints +=
										endPointRouteStatisticsInfo.RequestsCountFailed;
									requestsCountToEndPointFromEndPoints +=
										endPointRouteStatisticsInfo.RequestsCountToEndPoint;
									// !!!
									break;
								}
							}
						}
					}
				}
			}
		}

		////////////////////////////////////////////////
		// 4/，修正整体数据：
		// !!!⚠ 由于数据收集是异步的，因此可能出现同一秒钟，终点数据与路由器数据不一致的情况，所以需要进行修正（百分比合理） ⚠!!!
		////////////////////////////////////////////////
		{
			var routerRouteStatisticsInfo =
				routerStateStatisticsInfoInSecond.RouterRouteStatisticsInfo;
			if (routerRouteStatisticsInfo != null)
			{
				routerRouteStatisticsInfo.RequestsCount =
					requestsCountFromEndPoints;
				routerRouteStatisticsInfo.RequestsCountFailed =
					requestsCountFailedFromEndPoints;
				routerRouteStatisticsInfo.RequestsCountToEndPoint =
					requestsCountToEndPointFromEndPoints;
			}

			//  插入”全部终点“的路由信息：
			if (endPointRouteStatisticsInfesInSecond.Count > 0)
			{
				var allEndPointsStatisticsInfo = new RouteStatisticsInfo(
					RouteStatisticsInfoRecorder.CreateRouteStatisticsInfoNameWithRouterName(
						routerName,
						Router.EndPointName_All
					),
					routerName,
					Router.EndPointName_All,
					secondTime
				);
				{
					allEndPointsStatisticsInfo.RequestsCount =
						requestsCountFromEndPoints;
					allEndPointsStatisticsInfo.RequestsCountFailed =
						requestsCountFailedFromEndPoints;
					allEndPointsStatisticsInfo.RequestsCountToEndPoint =
						requestsCountToEndPointFromEndPoints;
				}
				endPointRouteStatisticsInfesInSecond.Add(
					Router.EndPointName_All,
					allEndPointsStatisticsInfo
				);
			}

			// 补充终点路由流量比例：
			var endPointRouteStatisticsInfes =
				endPointRouteStatisticsInfesInSecond.Values;
			if (endPointRouteStatisticsInfes?.Count > 0)
			{
				foreach (
					var endPointRouteStatisticsInfo in endPointRouteStatisticsInfes
				)
				{
					endPointRouteStatisticsInfo.RequestsCountRate =
						requestsCountFromEndPoints > 0
							? (
								(float)
									endPointRouteStatisticsInfo.RequestsCount
								/ requestsCountFromEndPoints
							)
							: 0.0F;

					endPointRouteStatisticsInfo.RequestsCountFailedRate =
						requestsCountFailedFromEndPoints > 0
							? (
								(float)
									endPointRouteStatisticsInfo.RequestsCountFailed
								/ requestsCountFailedFromEndPoints
							)
							: 0.0F;

					endPointRouteStatisticsInfo.RequestsCountToEndPointRate =
						requestsCountToEndPointFromEndPoints > 0
							? (
								(float)
									endPointRouteStatisticsInfo.RequestsCountToEndPoint
								/ requestsCountToEndPointFromEndPoints
							)
							: 0.0F;
				}
			}
		}
		// !!!
		routerStateStatisticsInfoInSecond.EndPointRouteStatisticsInfes =
			endPointRouteStatisticsInfesInSecond;
		// !!!

		return routerStateStatisticsInfoInSecond;
	}

	public static List<RouteStatisticsInfo>? GetRouteStatisticsInfesWithRouterName(
		string routerName,
		string? endPointName,
		DateTime beginTime,
		DateTime endTime
	)
	{
		if (
			beginTime >= endTime
			|| !_routerStatisticsInfoLists.TryGetValue(
				routerName,
				out var routerRouteStatisticsInfoList
			)
		)
		{
			return null;
		}
		var routeStatisticsInfoInTimeSection = new List<RouteStatisticsInfo>();
		lock (routerRouteStatisticsInfoList)
		{
			List<RouteStatisticsInfo>? originalRouteStatisticsInfoList =
				routerRouteStatisticsInfoList;
			if (
				endPointName?.Length > 0
				&& !endPointName.EqualsIgnoreCase(Router.EndPointName_All)
			)
			{
				//
				originalRouteStatisticsInfoList = null;
				//
				if (
					routerRouteStatisticsInfoList.EndPointRouteStatisticsInfes?.TryGetValue(
						endPointName,
						out var endPointRouteStatisticsInfoList
					) == true
				)
				{
					// !!!
					originalRouteStatisticsInfoList =
						endPointRouteStatisticsInfoList;
					// !!!
				}
			}
			if (originalRouteStatisticsInfoList?.Count > 0)
			{
				foreach (var routeStatisticsInfo in originalRouteStatisticsInfoList)
				{
					if (
						routeStatisticsInfo.RouteTimeInSecond >= beginTime
						&& routeStatisticsInfo.RouteTimeInSecond <= endTime
					)
					{
						routeStatisticsInfoInTimeSection.Add(
							routeStatisticsInfo
						);
					}
				}
			}
		}
		return routeStatisticsInfoInTimeSection;
	}

	public static List<RouteStatisticsInfoRecorder> GetExceptionResponseList(string routerName, string? endPointName, DateTime beginTime, DateTime endTime)
	{
		var validRouteTimeMin = DateTime.Now.AddSeconds(-1 * Config.Router.RouteStatisticsInfoRetainSeconds);
		return _exceptionResponseInfes.GetExceptionResponseList(routerName, endPointName, beginTime, endTime, validRouteTimeMin);
	}

	protected static void DidLogHttpRequest(HttpRequest request, ConnectionIpEndPoints? connectionIpEndPoints,
		HttpResponse response, RouteStatisticsInfoRecorder? routeStatisticsInfoRecorder)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(response);

		var logContentFormatter = Config.Router.HttpRequestLogContentFormatter;
		if (logContentFormatter == null || logContentFormatter.Length < 1)
		{
			logContentFormatter = RouterConfig.HttpRequestLogContentFormatterDefault;
		}
		if (logContentFormatter == null || logContentFormatter.Length < 1)
		{
			return;
		}

		const string dateTimeFormatter = "yyyy/MM/dd HH:mm:ss fff";

		var logContent = logContentFormatter;
		var httpRequestLogContentFormatterKeys = RouterConfig.HttpRequestLogContentFormatterKeys;
		if (httpRequestLogContentFormatterKeys != null)
		{
			foreach (var httpRequestLogContentFormatterKey in httpRequestLogContentFormatterKeys)
			{
				string? httpRequestLogContentFormatterValue = null;
				if ("RequestTime".EqualsIgnoreCase(httpRequestLogContentFormatterKey))
				{
					httpRequestLogContentFormatterValue = routeStatisticsInfoRecorder?.RouteTime.ToString(dateTimeFormatter);
				}
				else if ("RequestUrl".EqualsIgnoreCase(httpRequestLogContentFormatterKey))
				{
					httpRequestLogContentFormatterValue = request.Path;
				}
				else if ("RequestBodySize".EqualsIgnoreCase(httpRequestLogContentFormatterKey))
				{
					httpRequestLogContentFormatterValue = request.ContentLength.ToString();
				}
				else if ("ClientIP".EqualsIgnoreCase(httpRequestLogContentFormatterKey))
				{
					connectionIpEndPoints ??= request.GetConnectionIpEndPoints();
					httpRequestLogContentFormatterValue = connectionIpEndPoints.KeyIpEndPoint?.Address?.ToString();
				}
				else if ("ClientPort".EqualsIgnoreCase(httpRequestLogContentFormatterKey))
				{
					connectionIpEndPoints ??= request.GetConnectionIpEndPoints();
					httpRequestLogContentFormatterValue = (connectionIpEndPoints.KeyIpEndPoint?.Port ?? 0).ToString();
				}
				else if ("ClientUserAgent".EqualsIgnoreCase(httpRequestLogContentFormatterKey))
				{
					httpRequestLogContentFormatterValue = request.Headers.UserAgent;
				}
				else if ("ResponseTime".EqualsIgnoreCase(httpRequestLogContentFormatterKey))
				{
					httpRequestLogContentFormatterValue = routeStatisticsInfoRecorder?.ResponseTime.ToString(dateTimeFormatter);
				}
				else if ("ResponseSize".EqualsIgnoreCase(httpRequestLogContentFormatterKey))
				{
					httpRequestLogContentFormatterValue = response.ContentLength.ToString();
				}
				else if ("ResponseDurationSeconds".EqualsIgnoreCase(httpRequestLogContentFormatterKey))
				{
					httpRequestLogContentFormatterValue = routeStatisticsInfoRecorder != null
						? (routeStatisticsInfoRecorder.MillisecondsToResponse / 1000.0).ToString("f3") : null;
				}
				else if ("ResponseSendSeconds".EqualsIgnoreCase(httpRequestLogContentFormatterKey))
				{
					httpRequestLogContentFormatterValue = routeStatisticsInfoRecorder != null
						? (routeStatisticsInfoRecorder.MillisecondsToSendResponse / 1000.0).ToString("f3") : null;
				}
				else if ("RouterDurationSeconds".EqualsIgnoreCase(httpRequestLogContentFormatterKey))
				{
					httpRequestLogContentFormatterValue = routeStatisticsInfoRecorder != null
						? (routeStatisticsInfoRecorder.MillisecondsToRoute / 1000.0).ToString("f3") : null;
				}
				else if ("EndPointDurationSeconds".EqualsIgnoreCase(httpRequestLogContentFormatterKey))
				{
					httpRequestLogContentFormatterValue = routeStatisticsInfoRecorder != null
						? (routeStatisticsInfoRecorder.MillisecondsToGetOriginalResponseCache / 1000.0).ToString("f3") : null;
				}
				if (httpRequestLogContentFormatterValue == null || httpRequestLogContentFormatterValue.Length < 1)
				{
					httpRequestLogContentFormatterValue = "未知";
				}
				logContent = logContent.Replace(
					httpRequestLogContentFormatterKey, httpRequestLogContentFormatterValue, StringComparison.OrdinalIgnoreCase);
			}
		}
		if (logContent?.Length > 0)
		{
			Log.HttpRequest.Logs(nameof(Router), logContent, null);
		}
	}

	public static int GetAllResponseCachesCount()
	{
		return _clientResponseCaches.GetAllResponseCachesCount();
	}

	public static async Task<int> RemoveResponseCachesWithRequestRelativePathAsync(
		string requestRelativePath
	)
	{
		return await _clientResponseCaches.RemoveResponseCachesWithRequestRelativePathAsync(
			requestRelativePath
		);
	}

	public static int RemoveAllResponseCaches()
	{
		return _clientResponseCaches.Clear();
	}

	#endregion


	////////////////////////////////////////////////
	// @私有属性
	////////////////////////////////////////////////

	protected CancellationTokenSource _operationCancellationTokenSource = new();

	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	/// <summary>
	/// 路由名称，即路由规则名称。
	/// </summary>
	public string? Name => RouteRule.Name;

	/// <summary>
	/// 当前路由器绑定的路由规则。
	/// </summary>
	public RouteRule RouteRule { get; set; } = RouteRule;

#pragma warning disable IDE0079 // 请删除不必要的忽略
#pragma warning disable CA1821 // 移除空终结器
	~Router()
#pragma warning restore CA1821 // 移除空终结器
#pragma warning restore IDE0079 // 请删除不必要的忽略
	{
		//!!!⚠ 不要在“业务，规则参数释放的时候”，释放相关的网络资源，传输还在进行中 ⚠!!!
		// !!!
		// _operationCancellationTokenSource.Cancel();
		// !!!
	}

	/// <summary>
	/// 当前路由是否可用，且匹配指定的请求信息。
	/// </summary>
	/// <param name="clientRequestPathInfo">客户端请求信息。</param>
	/// <returns>当匹配客户端请求时，返回：true，如果没有匹配，则返回：false。</returns>
	public bool IsEnableToMatchedClientRequestPathInfo(ClientRequestPathInfo? clientRequestPathInfo, DateTime routeTime)
	{
		var routeRule = this.RouteRule;
		if (routeRule?.IsEnableToMatchedClientRequestPathInfo(clientRequestPathInfo, routeTime) != true)
		{
			// !!! ⚠不拦截没有命中的路由请求，可能是有效的控制器请求，如果不是控制器请求，同样会触发程序异常。⚠!!!
			return false;
			// !!!
		}
		return true;
	}

	/// <summary>
	/// 根据指定的请求信息，创建客户端请求信息对象。
	/// </summary>
	/// <param name="clientRequest">客户端Http请求对象。</param>
	/// <param name="clientRequestPathInfo">客户端请求路径信息。</param>
	/// <returns>由指定的请求信息，创建的客户端请求信息对象</returns>
	public async Task<RequestInfo> CreateClientRequestInfoAsync(HttpRequest clientRequest, ClientRequestPathInfo clientRequestPathInfo)
	{
		var clientRequestInfo = await this.RouteRule.CreateClientRequestInfoAsync(
			clientRequest,
			clientRequestPathInfo
		);
		{ }
		return clientRequestInfo;
	}

	/// <summary>
	/// 根据指定Http请求信息，客户端请求信息，创建对应的终点请求信息。
	/// </summary>
	/// <param name="request">Http请求信息。</param>
	/// <param name="clientRequestPathInfo">客户端请求信息。</param>
	/// <param name="routeTime">本次路由时间。</param>
	/// <returns>对应指定Http请求信息，客户端请求信息的终点请求信息。</returns>
	public EndPointRequestInfo? CreateEndPointRequestInfo(
		HttpRequest request, ClientRequestPathInfo clientRequestPathInfo, RequestInfo clientRequestInfo, DateTime routeTime)
	{
		return this.DidCreateEndPointRequestInfo(
			request,
			clientRequestPathInfo,
			clientRequestInfo,
			routeTime
		);
	}

	/// <summary>
	/// 路由请求。
	/// </summary>
	/// <param name="request">要进行路由操作的来路请求对象。</param>
	/// <param name="routeTime">当前路由时间。</param>
	/// <param name="clientIp">客户端的Ip地址。</param>
	/// <param name="httpContext">Http请求上下文，常用于处理WebSockets请求。</param>
	/// <param name="clientRequestPathInfo">客户端请求路径信息。</param>
	/// <param name="routeRule">请求匹配到的路由规则。</param>
	/// <param name="endPoint">请求匹配到的终点对象</param>
	/// <param name="endPointRequestInfo">路由到终点的请求信息。</param>
	/// <param name="response">要进行响应的来路请求响应对象。</param>
	/// <param name="routeStatisticsInfoRecorder">路由统计信息记录器。</param>
	/// <returns>路由请求成功时，返回：true，否则返回：false。</returns>
	/// <exception cref="TimeoutException">终点响应超时后，会引发“TimeoutException”异常。</exception>
	public async Task<bool> RouteRequest(HttpRequest request, DateTime routeTime, ConnectionIpEndPoints? connectionIpEndPoints,
		HttpContext httpContext, ClientRequestPathInfo clientRequestPathInfo, RouteRule routeRule, EndPoint endPoint,
		EndPointRequestInfo endPointRequestInfo, HttpResponse response, RouteStatisticsInfoRecorder routeStatisticsInfoRecorder)
	{
		return await this.DidRouteRequest(request, routeTime, connectionIpEndPoints, httpContext, clientRequestPathInfo, routeRule,
			endPoint, endPointRequestInfo, response, routeStatisticsInfoRecorder);
	}

	////////////////////////////////////////////////
	// @事件节点
	////////////////////////////////////////////////

	/// <summary>
	/// 根据指定Http请求信息，客户端请求信息，创建对应的终点请求信息。
	/// </summary>
	/// <param name="request">Http请求信息。</param>
	/// <param name="clientRequestPathInfo">客户端请求信息。</param>
	/// <param name="routeTime">本次路由时间。</param>
	/// <returns>对应指定Http请求信息，客户端请求信息的终点请求信息。</returns>
	public EndPointRequestInfo? DidCreateEndPointRequestInfo(
		HttpRequest request,
		ClientRequestPathInfo clientRequestPathInfo,
		RequestInfo clientRequestInfo,
		DateTime routeTime
	)
	{
		var endPointRequestInfo = this.RouteRule?.CreateEndPointRequestInfo(
			request,
			clientRequestPathInfo,
			clientRequestInfo,
			routeTime
		);
		{ }
		return endPointRequestInfo;
	}

	/// <summary>
	/// 路由请求。
	/// </summary>
	/// <param name="request">要进行路由操作的来路请求对象。</param>
	/// <param name="routeTime">当前路由时间。</param>
	/// <param name="clientIp">客户端的Ip地址。</param>
	/// <param name="httpContext">Http请求上下文，常用于处理WebSockets请求。</param>
	/// <param name="clientRequestPathInfo">客户端请求路径信息。</param>
	/// <param name="routeRule">请求匹配到的路由规则。</param>
	/// <param name="endPoint">请求匹配到的终点对象</param>
	/// <param name="endPointRequestInfo">路由到终点的请求信息。</param>
	/// <param name="response">要进行响应的来路请求响应对象。</param>
	/// <param name="routeStatisticsInfoRecorder">路由统计信息记录器。</param>
	/// <returns>路由请求成功时，返回：true，否则返回：false。</returns>
	/// <exception cref="TimeoutException">终点响应超时后，会引发“TimeoutException”异常。</exception>
	protected async Task<bool> DidRouteRequest(
		HttpRequest request, DateTime routeTime, ConnectionIpEndPoints? connectionIpEndPoints, HttpContext httpContext,
		ClientRequestPathInfo clientRequestPathInfo, RouteRule routeRule, EndPoint endPoint, EndPointRequestInfo endPointRequestInfo,
		HttpResponse response, RouteStatisticsInfoRecorder routeStatisticsInfoRecorder
	)
	{
		// !!!⚠ 峰值处理： ⚠!!!
		var responseHttpStatusCodeSpecified = 0;
		if (endPoint != null)
		{
			var rpsMax = endPoint.RPSMax;
			if (rpsMax <= 0.0F)
			{
				rpsMax = Config.Router.EndPointRPSMax;
			}
			if (rpsMax > 0)
			{
				var requestTimeoutSeconds = Config
					.Router
					.EndPointRequestTimeoutSecondsMax;
				if (
					routeRule != null
					&& routeRule.EndPointRequestTimeoutSeconds > 0
					&& routeRule.EndPointRequestTimeoutSeconds
						< requestTimeoutSeconds
				)
				{
					requestTimeoutSeconds =
						routeRule.EndPointRequestTimeoutSeconds;
				}
				var requestTimeoutTime =
					requestTimeoutSeconds > 0.0
						? routeTime.AddSeconds(requestTimeoutSeconds)
						: DateTime.MaxValue;

				var isNeedWaittingRequestsCountDecrease = true;
				while (isNeedWaittingRequestsCountDecrease)
				{
					// 1/2，100% 精准，但是性能相对较低：
					//lock (endPoint)
					{
						// 当前与统计信息在同一秒，则根据当前秒中的请求数选择请求：
						var now = DateTime.Now;
						if (now.EqualsInSecond(endPoint.RPSStatisticsTime))
						{
							// !!! 注意这里一定是“<”，“==rpsMax”表示当前终点满载 !!!
							if (endPoint.RPSValue < rpsMax)
							{
								// !!!
								Interlocked.Increment(
									ref endPoint.RPSValue
								);
								isNeedWaittingRequestsCountDecrease =
									false;
								// !!!
							}
						}
						// 当前与统计信息不在同一秒，则更新当前最后一次请求时间：
						else
						{
							// !!!
							endPoint.RPSStatisticsTime = now;
							endPoint.RPS = 1;
							isNeedWaittingRequestsCountDecrease = false;
							// !!!
						}
					}

					if (isNeedWaittingRequestsCountDecrease)
					{
						var endPointHttpStatusCodeWhenRPSOverMax =
							endPoint.HttpStatusCodeWhenRPSOverMax;
						if (endPointHttpStatusCodeWhenRPSOverMax > 0)
						{
							responseHttpStatusCodeSpecified =
								endPointHttpStatusCodeWhenRPSOverMax;
						}
						else
						{
							// !!!
							await Task.Delay(
								1000,
								_operationCancellationTokenSource.Token
							);
							// !!! 注意这里要重新获取时间 !!!
							if (DateTime.Now >= requestTimeoutTime)
							{
								// !!!
								throw new TimeoutException();
								// !!!
							}
						}
					}
				}
			}

			// #路由统计#
			routeStatisticsInfoRecorder.EndWaitingForRPSMax();
		}

		bool routeResult;
		if (httpContext.WebSockets.IsWebSocketRequest)
		{
			routeResult = await DidRouteRequestByMatchedRouteRuleWithWebSocketsRequest(
				request, endPointRequestInfo, connectionIpEndPoints, httpContext, clientRequestPathInfo, response, routeStatisticsInfoRecorder);
		}
		else
		{
			routeResult = await DidRouteRequestByMatchedRouteRuleWithHttpRequest(
				request, endPointRequestInfo, connectionIpEndPoints, httpContext, response, responseHttpStatusCodeSpecified, routeStatisticsInfoRecorder);
		}
		return routeResult;
	}

	/// <summary>
	/// 路由请求，Http请求。
	/// </summary>
	/// <param name="request">要进行路由操作的来路请求对象。</param>
	/// <param name="endPointRequestInfo">路由后的请求信息。</param>
	/// <param name="clientIp">客户端的Ip地址。</param>
	/// <param name="httpContext">Http请求上下文，常用于处理WebSockets请求。</param>
	/// <param name="requestPath">要进行路由操作的来路请求路径。</param>
	/// <param name="response">要进行响应的来路请求响应对象。</param>
	/// <param name="responseHttpStatusCodeSpecified">指定的Http响应状态码。</param>
	/// <param name="routeStatisticsInfoRecorder">路由统计信息记录器。</param>
	/// <returns>返回路由结果（RouteResult）。</returns>
	protected async Task<bool> DidRouteRequestByMatchedRouteRuleWithHttpRequest(
		HttpRequest request,
		EndPointRequestInfo endPointRequestInfo,
		ConnectionIpEndPoints? connectionIpEndPoints,
		HttpContext httpContext,
		//ClientRequestPathInfo requestPath,
		HttpResponse response,
		int responseHttpStatusCodeSpecified,
		RouteStatisticsInfoRecorder routeStatisticsInfoRecorder
	)
	{
		if (responseHttpStatusCodeSpecified <= 0
			&& endPointRequestInfo.EndPointResponseForwardMode
			== EndPointResponseForwardMode.ServerSentEvents)
		{
			return await this.DidRouteRequestByMatchedRouteRuleWithServerSentEventsRequestAsync(
				endPointRequestInfo, connectionIpEndPoints, httpContext, response, routeStatisticsInfoRecorder);
		}
		if (responseHttpStatusCodeSpecified <= 0
			&& endPointRequestInfo.EndPointResponseForwardMode
			== EndPointResponseForwardMode.ForwardByBytes)
		{
			return await this.DidRouteRequestByMatchedRouteRuleWithHttpRequestByBytesAsync(
				request, endPointRequestInfo, connectionIpEndPoints, httpContext, response, routeStatisticsInfoRecorder);
		}

		ClientResponseCache? clientResponseCacheShot = null;
		Exception? clientResponseCacheShotException = null;

		try
		{
			var endPoint = endPointRequestInfo.EndPoint;
			/* !!!⚠ 开始路由后，就不再进行终点的有效性判断 ⚠!!!
				     if (endPoint != null
					 && endPoint.IsConnectionEnable == false)
				     {
					 // !!!
					 throw new Exception(
					     ExceptionMessages.RequestToEndPointFailed_IsConnectionDisable);
					 // !!!
				     }
				     */
			if (responseHttpStatusCodeSpecified > 0)
			{
				clientResponseCacheShot = new ClientResponseCache()
				{
					ResponseHttpStatusCode = responseHttpStatusCodeSpecified,
					ResponseRouteResponseType = HttpHeaderValues.RouteResponseTypes.CacheData,
				};
				routeStatisticsInfoRecorder.IsResponseGetFromEndPoint = false;
			}
			else
			{
				// !!! ⚠
				clientResponseCacheShot = await _clientResponseCaches.ShootResponseCacheWithRequestInfoAsync(
					endPointRequestInfo, connectionIpEndPoints, routeStatisticsInfoRecorder);
				// !!! ⚠
			}
		}
		catch (Exception exception)
		{
			clientResponseCacheShotException = exception;

			if (endPointRequestInfo.EndPointResponseExceptionProcess == ResponseExceptionProcess.ReturnLastResponseCache)
			{
				// !!! ⚠
				clientResponseCacheShot = await _clientResponseCaches.TryGetResponseCacheWithRequestInfoAsync(endPointRequestInfo);
				clientResponseCacheShot?.ResponseRouteResponseType = HttpHeaderValues.RouteResponseTypes.CacheData;
				// !!! ⚠
			}
		}

		// #路由统计#
		if (clientResponseCacheShotException != null
			|| routeStatisticsInfoRecorder.IsResponseGetFromEndPoint)
		{
			routeStatisticsInfoRecorder.EndGetOriginalResponseCache();
		}

		////////////////////////////////////////////////
		// !!!⚠ 向客户端发送响应信息 ⚠!!!
		////////////////////////////////////////////////
		if (clientResponseCacheShot != null && response.HasStarted == false)
		{
			await response.WriteResponseCacheForRequestAsync(request, clientResponseCacheShot, routeStatisticsInfoRecorder);
			// !!!
			// !!!
			await response.CompleteAsync();
			// !!!
		}
		////////////////////////////////////////////////
		////////////////////////////////////////////////

		// #路由统计#
		routeStatisticsInfoRecorder.EndSendResponse();

		if (clientResponseCacheShotException != null)
		{
			throw clientResponseCacheShotException;
		}

		return true;
	}

	/// <summary>
	/// 路由请求，按指定字节数将终点响应流式转发给客户端。
	/// </summary>
	protected async Task<bool> DidRouteRequestByMatchedRouteRuleWithHttpRequestByBytesAsync(
		HttpRequest request,
		EndPointRequestInfo endPointRequestInfo,
		ConnectionIpEndPoints? connectionIpEndPoints,
		HttpContext httpContext,
		HttpResponse response,
		RouteStatisticsInfoRecorder routeStatisticsInfoRecorder)
	{
		Exception? endPointResponseException = null;
		ClientResponseCache? clientResponseCacheShot = null;
		routeStatisticsInfoRecorder.IsResponseGetFromEndPoint = true;
		var applicationStopping = httpContext.RequestServices
			.GetService<IHostApplicationLifetime>()?
			.ApplicationStopping ?? CancellationToken.None;

		try
		{
			using var requestCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
				_operationCancellationTokenSource.Token, httpContext.RequestAborted, applicationStopping);
			if (endPointRequestInfo.EndPointRequestTimeoutSeconds > 0)
			{
				requestCancellationTokenSource.CancelAfter(
					(int)(endPointRequestInfo.EndPointRequestTimeoutSeconds * 1000));
			}

			using var endPointRequestMessage
				= ClientResponseCache.CreateRequestMessageToUrl(
					endPointRequestInfo.EndPointRequestUrl,
					endPointRequestInfo.RequestHttpHeader,
					endPointRequestInfo.RequestBodyBytes,
					connectionIpEndPoints)
				?? throw new ApplicationException("创建终点请求失败。");
			using var endPointResponseMessage
				= await ClientResponseCaches.HttpClient.SendAsync(
					endPointRequestMessage,
					HttpCompletionOption.ResponseHeadersRead,
					requestCancellationTokenSource.Token);

			var validHttpStatusCodesToCache
				= Config.Router.EndPointResponseCacheValidHttpStatusCodes;
			if (validHttpStatusCodesToCache?.Contains(
				(int)endPointResponseMessage.StatusCode) != true)
			{
				var exceptionTitle = ExceptionMessages
					.RequestToEndPointFailed_InvalidHttpStatusCodes
					.Replace("#终点Url#", endPointRequestInfo.EndPointRequestUrl)
					.Replace("#状态码#", ((int)endPointResponseMessage.StatusCode).ToString());
				throw new HttpRequestException(
					exceptionTitle,
					null,
					endPointResponseMessage.StatusCode);
			}

			var isEndPointResponseCompleted = await response.WriteEndPointResponseByBytesAndGetCompletionStateAsync(
				endPointResponseMessage, endPointRequestInfo.EndPointName, endPointRequestInfo.EndPointResponseForwardBytesCount,
					routeStatisticsInfoRecorder, requestCancellationTokenSource.Token);
			if (isEndPointResponseCompleted
				&& requestCancellationTokenSource.IsCancellationRequested != true)
			{
				await response.CompleteAsync();
			}
		}
		catch (OperationCanceledException)
		when (httpContext.RequestAborted.IsCancellationRequested || applicationStopping.IsCancellationRequested)
		{
			// 客户端主动断开连接，不应作为终点请求失败计入熔断统计。
		}
		catch (IOException)
		when (httpContext.RequestAborted.IsCancellationRequested || applicationStopping.IsCancellationRequested)
		{
			// 客户端断开时，响应流也可能由底层连接抛出 IOException。
		}
		catch (Exception exception)
		{
			endPointResponseException = exception;
			if (endPointRequestInfo.EndPointResponseExceptionProcess
				== ResponseExceptionProcess.ReturnLastResponseCache)
			{
				clientResponseCacheShot = await _clientResponseCaches
					.TryGetResponseCacheWithRequestInfoAsync(endPointRequestInfo);
				clientResponseCacheShot?.ResponseRouteResponseType
					= HttpHeaderValues.RouteResponseTypes.CacheData;
			}
		}

		routeStatisticsInfoRecorder.EndGetOriginalResponseCache();
		if (clientResponseCacheShot != null && response.HasStarted == false)
		{
			await response.WriteResponseCacheForRequestAsync(
				request,
				clientResponseCacheShot,
				routeStatisticsInfoRecorder);
			await response.CompleteAsync();
		}
		routeStatisticsInfoRecorder.EndSendResponse();

		if (endPointResponseException != null)
		{
			throw endPointResponseException;
		}

		return true;
	}

	/// <summary>
	/// 路由请求，将终点的 Server-Sent Events 响应实时转发给客户端。
	/// </summary>
	protected async Task<bool> DidRouteRequestByMatchedRouteRuleWithServerSentEventsRequestAsync(
		EndPointRequestInfo endPointRequestInfo,
		ConnectionIpEndPoints? connectionIpEndPoints,
		HttpContext httpContext,
		HttpResponse response,
		RouteStatisticsInfoRecorder routeStatisticsInfoRecorder)
	{
		routeStatisticsInfoRecorder.IsResponseGetFromEndPoint = true;
		var applicationStopping = httpContext.RequestServices
			.GetService<IHostApplicationLifetime>()?
			.ApplicationStopping ?? CancellationToken.None;

		using var requestCancellationTokenSource
			= CancellationTokenSource.CreateLinkedTokenSource(
			_operationCancellationTokenSource.Token,
			httpContext.RequestAborted,
			applicationStopping);
		try
		{
			using var endPointRequestMessage
				= ClientResponseCache.CreateRequestMessageToUrl(
					endPointRequestInfo.EndPointRequestUrl,
					endPointRequestInfo.RequestHttpHeader,
					endPointRequestInfo.RequestBodyBytes,
					connectionIpEndPoints)
				?? throw new ApplicationException("创建终点请求失败。");
			endPointRequestMessage.Headers.Remove("Accept-Encoding");
			endPointRequestMessage.Headers.TryAddWithoutValidation(
				"Accept-Encoding",
				HttpHeaderValues.Content_Encodings.Identity);

			HttpResponseMessage endPointResponseMessage;
			using (var responseHeadersCancellationTokenSource
				= CancellationTokenSource.CreateLinkedTokenSource(
					requestCancellationTokenSource.Token))
			{
				if (endPointRequestInfo.EndPointRequestTimeoutSeconds > 0)
				{
					// SSE 建立连接后可以长期存在，此超时只约束等待终点响应头的时间。
					responseHeadersCancellationTokenSource.CancelAfter(
						(int)(endPointRequestInfo.EndPointRequestTimeoutSeconds * 1000));
				}
				endPointResponseMessage
					= await ClientResponseCaches.HttpClient.SendAsync(
						endPointRequestMessage,
						HttpCompletionOption.ResponseHeadersRead,
						responseHeadersCancellationTokenSource.Token);
			}

			using (endPointResponseMessage)
			{
				// 响应头已到达，先结束终点握手耗时统计，再校验 SSE 协议头。
				routeStatisticsInfoRecorder.EndGetOriginalResponseCache();
				if (endPointResponseMessage.StatusCode == HttpStatusCode.OK
					&& !"text/event-stream".Equals(
						endPointResponseMessage.Content?.Headers.ContentType?.MediaType,
						StringComparison.OrdinalIgnoreCase))
				{
					throw new HttpRequestException(
						"终点响应的 Content-Type 不是 text/event-stream。",
						null,
						HttpStatusCode.BadGateway);
				}

				var isEndPointResponseCompleted
					= await response.WriteEndPointResponseWithServerSentEventsAsync(
						endPointResponseMessage,
						endPointRequestInfo.EndPointName,
						endPointRequestInfo.EndPointResponseForwardBytesCount,
						routeStatisticsInfoRecorder,
						requestCancellationTokenSource.Token);
				if (isEndPointResponseCompleted
					&& requestCancellationTokenSource.IsCancellationRequested != true)
				{
					await response.CompleteAsync();
				}
			}
		}
		catch (OperationCanceledException)
			when (httpContext.RequestAborted.IsCancellationRequested
				|| applicationStopping.IsCancellationRequested)
		{
			// EventSource 或流式客户端主动关闭连接属于正常生命周期。
			return true;
		}
		catch (IOException)
			when (httpContext.RequestAborted.IsCancellationRequested
				|| applicationStopping.IsCancellationRequested)
		{
			// 客户端断开时，响应流也可能由底层连接抛出 IOException。
			return true;
		}

		return true;
	}

	/// <summary>
	/// 根据指定的请求信息，创建对应的WebSocket。
	/// </summary>
	/// <param name="endPointRequestInfo">根据指定的请求信息。</param>
	/// <returns>根据指定的请求信息，创建的WebSocket</returns>
	protected ClientWebSocket DidCreateClientWebSocketWithRequestInfo(
#pragma warning disable IDE0060 // 删除未使用的参数
		EndPointRequestInfo endPointRequestInfo
	)
#pragma warning restore IDE0060 // 删除未使用的参数
	{
		ClientWebSocket socket = new();
		//var socketOptions = socket.Options;

		// !!!⚠ 禁用WebSocket头部转发，WSS协议时转发请求头会导致浏览器报送1006错误。 ⚠!!!
		//var requestHttpHeader = endPointRequestInfo.RequestHttpHeader;
		//if (requestHttpHeader?.Count > 0)
		//{
		//        foreach (var headerKeyValue in requestHttpHeader)
		//        {
		//                socketOptions.SetRequestHeader(
		//                        headerKeyValue.Key,
		//                        headerKeyValue.Value);
		//        }
		//}

		//var clientCookies = clientSocketOptions.Cookies;
		//if (clientCookies?.Count > 0)
		//{
		//        socketOptions.Cookies = clientCookies;
		//}

		return socket;
	}

	/// <summary>
	/// 路由请求，WebSockets请求。
	/// </summary>
	/// <param name="request">要进行路由操作的来路请求对象。</param>
	/// <param name="endPointRequestInfo">路由后的请求信息。</param>
	/// <param name="clientIp">客户端的Ip地址。</param>
	/// <param name="httpContext">Http请求上下文，常用于处理WebSockets请求。</param>
	/// <param name="requestPath">要进行路由操作的来路请求路径。</param>
	/// <param name="response">要进行响应的来路请求响应对象。</param>
	/// <param name="routeStatisticsInfoRecorder">路由统计信息记录器。</param>
	/// <returns>返回路由结果（RouteResult）。</returns>
	protected async Task<bool> DidRouteRequestByMatchedRouteRuleWithWebSocketsRequest(
#pragma warning disable IDE0060 // 删除未使用的参数
		HttpRequest request,
		EndPointRequestInfo endPointRequestInfo,
		ConnectionIpEndPoints? connectionIpEndPoints,
		HttpContext httpContext,
		ClientRequestPathInfo clientRequestPathInfo,
		HttpResponse response,
		RouteStatisticsInfoRecorder routeStatisticsInfoRecorder)
#pragma warning restore IDE0060 // 删除未使用的参数
	{
		var socketToClient = clientRequestPathInfo.ClientWebSocketAccepted;
		if (socketToClient == null)
		{
			socketToClient = await httpContext.WebSockets.AcceptWebSocketAsync();
			clientRequestPathInfo.ClientWebSocketAccepted = socketToClient;
		}

		if (string.IsNullOrEmpty(endPointRequestInfo.EndPointRequestUrl))
		{
			// !!!⚠ 异常情况，不应当出现 ⚠!!!
			throw new ApplicationException("通过WebSocket连接终点失败， 无效的终点请求路径。")
			{
				Data = { { "endPointRequestInfo", endPointRequestInfo } },
			};
			//await socketToClient.CloseAsync(
			//        WebSocketCloseStatus.EndpointUnavailable,
			//        "无效的终点请求路径。",
			//        _operationCancellationTokenSource.Token);
			//return false;
		}

		// !!!
		using var socketToEndPoint = this.DidCreateClientWebSocketWithRequestInfo(
			endPointRequestInfo
		);
		// !!!
		await socketToEndPoint.ConnectAsync(
			new Uri(endPointRequestInfo.EndPointRequestUrl),
			_operationCancellationTokenSource.Token
		);
		// !!!

		////////////////////////////////////////////////
		// 客户端和服务端的请求结果：
		////////////////////////////////////////////////
		WebSocketReceiveResult? responseBodyFromEndPointReceiveResult = null;
		WebSocketReceiveResult? requestBodyFromClientReceiveResult = null;

		////////////////////////////////////////////////
		// 1/4，开启线程，异步完成：服务端 -> 客户端 的转发通道：
		////////////////////////////////////////////////
		var taskToProcessEndPointResponse = Task.Run(async () =>
		{
			var dataBytesBuffer = new BytesBuffer(
				WebSockets_RequestBodyBufferSizeInByte
			);
			while (
				(
					responseBodyFromEndPointReceiveResult == null
					|| responseBodyFromEndPointReceiveResult.CloseStatus == null
				)
				&& (
					requestBodyFromClientReceiveResult == null
					|| requestBodyFromClientReceiveResult.CloseStatus == null
				)
			)
			{
				////////////////////////////////////////////////
				// 1/2，接收终点请求：
				////////////////////////////////////////////////
				ArraySegment<byte>? responseBodyBytes = null;
				dataBytesBuffer.Clear();
				do
				{
					var responseBodyBufferBytes =
						dataBytesBuffer.GetEmptyBufferSegment();
					responseBodyFromEndPointReceiveResult =
						await socketToEndPoint.ReceiveAsync(
							responseBodyBufferBytes,
							_operationCancellationTokenSource.Token
						);
					// !!!
					dataBytesBuffer.BytesCount +=
						responseBodyFromEndPointReceiveResult.Count;
					// !!!
				} while (
					responseBodyFromEndPointReceiveResult?.EndOfMessage == false
				);
				// !!!
				responseBodyBytes = dataBytesBuffer.Bytes;
				// !!!

				////////////////////////////////////////////////
				// 2/2，同步完成：转发终点响应到客户端：
				////////////////////////////////////////////////
				if (
					responseBodyFromEndPointReceiveResult != null
					&& responseBodyBytes?.Count > 0
				)
				{
					await socketToClient.SendAsync(
						responseBodyBytes.Value,
						responseBodyFromEndPointReceiveResult.MessageType,
						true,
						_operationCancellationTokenSource.Token
					);
				}
			}
		});

		////////////////////////////////////////////////
		// 2/4，同步完成：客户端 -> 服务端 的转发通道：
		////////////////////////////////////////////////
		{
			var dataBytesBuffer = new BytesBuffer(
				WebSockets_RequestBodyBufferSizeInByte
			);
			while (
				(
					responseBodyFromEndPointReceiveResult == null
					|| responseBodyFromEndPointReceiveResult.CloseStatus == null
				)
				&& (
					requestBodyFromClientReceiveResult == null
					|| requestBodyFromClientReceiveResult.CloseStatus == null
				)
			)
			{
				////////////////////////////////////////////////
				// 1/2，接收客户端请求：
				////////////////////////////////////////////////
				ArraySegment<byte>? requestBodyBytes = null;
				dataBytesBuffer.Clear();
				do
				{
					var requestBodyBufferBytes = dataBytesBuffer.GetEmptyBufferSegment();
					requestBodyFromClientReceiveResult = await socketToClient.ReceiveAsync(
						requestBodyBufferBytes, _operationCancellationTokenSource.Token);
					// !!!
					dataBytesBuffer.BytesCount += requestBodyFromClientReceiveResult.Count;
					// !!!
				} while (requestBodyFromClientReceiveResult?.EndOfMessage == false);
				// !!!
				requestBodyBytes = dataBytesBuffer.Bytes;
				// !!!

				////////////////////////////////////////////////
				// 2/2，转发客户端请求到终点：
				////////////////////////////////////////////////
				if (requestBodyFromClientReceiveResult != null && requestBodyBytes?.Count > 0)
				{
					await socketToEndPoint.SendAsync(requestBodyBytes.Value, requestBodyFromClientReceiveResult.MessageType,
						true, _operationCancellationTokenSource.Token);
				}
			}
		}

		////////////////////////////////////////////////
		// 3/4，!!! 等待处理服务端响应的任务完成 !!!
		await taskToProcessEndPointResponse;
		// !!!
		////////////////////////////////////////////////

		////////////////////////////////////////////////
		// 4/4，连接结束：
		////////////////////////////////////////////////

		WebSocketCloseStatus socketCloseStatus = WebSocketCloseStatus.NormalClosure;
		string? socketCloseStatusDescription = null;
		if (
			requestBodyFromClientReceiveResult != null
			&& requestBodyFromClientReceiveResult.CloseStatus != null
		)
		{
			socketCloseStatus = requestBodyFromClientReceiveResult.CloseStatus.Value;
			socketCloseStatusDescription =
				requestBodyFromClientReceiveResult.CloseStatusDescription;
			// 由客户端关闭的连接，需要“应答”客户端发送的关闭请求：
			await socketToClient.CloseOutputAsync(
				WebSocketCloseStatus.NormalClosure,
				null,
				_operationCancellationTokenSource.Token
			);
			// 由客户端关闭的连接，需要手动关闭去向终点的Socket：
			await socketToEndPoint.CloseAsync(
				socketCloseStatus,
				socketCloseStatusDescription,
				_operationCancellationTokenSource.Token
			);
		}
		else if (
			responseBodyFromEndPointReceiveResult != null
			&& responseBodyFromEndPointReceiveResult.CloseStatus != null
		)
		{
			socketCloseStatus = responseBodyFromEndPointReceiveResult.CloseStatus.Value;
			socketCloseStatusDescription =
				responseBodyFromEndPointReceiveResult.CloseStatusDescription;
			// 由客户端关闭的连接，需要“应答”服务端发送的关闭请求：
			await socketToEndPoint.CloseOutputAsync(
				socketCloseStatus,
				socketCloseStatusDescription,
				_operationCancellationTokenSource.Token
			);
			// 由终点关闭的连接，需要手动关闭去向客户端的Socket：
			await socketToClient.CloseAsync(
				socketCloseStatus,
				socketCloseStatusDescription,
				_operationCancellationTokenSource.Token
			);
		}

		// !!! 手动关闭新建的，去向终点的Socket !!!
		socketToEndPoint.Dispose();
		// !!!

		return true;
	}

	////////////////////////////////////////////////
	// @重载
	////////////////////////////////////////////////

	#region 重载


	public void Dispose()
	{
		//!!!⚠ 不要在“业务，规则参数释放的时候”，释放相关的网络资源，传输还在进行中 ⚠!!!
		// !!!
		// _operationCancellationTokenSource.Cancel();
		// !!!

		GC.SuppressFinalize(this);
	}

	#endregion
}
