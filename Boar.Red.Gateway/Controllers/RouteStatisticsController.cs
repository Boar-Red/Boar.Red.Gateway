using BaoXia.Constants;
using BaoXia.Utils;
using BaoXia.Utils.Extensions;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Extensions;
using Boar.Red.Gateway.LogFiles;
using Boar.Red.Gateway.Utils;
using Boar.Red.Gateway.ViewModels;
using Boar.Red.Service.Attributes;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Boar.Red.Gateway.Controllers;

public class RouteStatisticsController : Controller
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[ServiceAdministratorApi]
	public IActionResult Index()
	{
		var response = new RouteStatisticsIndexResponse();
		try
		{
			var allRouteNames = new List<string>();
			{
				allRouteNames.Add(Router.RouterName_All);
			}
			var routerEndPointInfes = new Dictionary<string, ICollection<EndPointStateInfo>>();
			var allEndPointInfes = new List<EndPointStateInfo>();
			{
				allEndPointInfes.Add(
				    new EndPointStateInfo(
				    Router.EndPointName_All,
				    true));
			}
			routerEndPointInfes.Add(Router.RouterName_All, allEndPointInfes);

			var routeRules = Config.Router.RouteRules;
			if (routeRules?.Length > 0)
			{
				foreach (var routeRule in routeRules)
				{
					var routeName = routeRule.Name;
					if (routeName?.Length > 0)
					{
						//
						allRouteNames.Add(routeName);
						//
						var endPoints = routeRule.EndPoints;
						if (endPoints?.Length > 0)
						{
							var endPointInfes = new List<EndPointStateInfo>();
							{
								endPointInfes.Add(new EndPointStateInfo(
								    Router.EndPointName_All,
								    true,
								    default));
							}
							foreach (var endPoint in endPoints)
							{
								// !!!
								allEndPointInfes.AddUnique(new EndPointStateInfo(endPoint));
								endPointInfes.AddUnique(new EndPointStateInfo(endPoint));
								// !!!
							}
							routerEndPointInfes.Add(routeName, endPointInfes);
						}
					}
				}
			}
			response.AllRouterNames = allRouteNames;
			response.RouterEndPointInfes = routerEndPointInfes;
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(this, "执行失败，程序异常。", exception);
			//
			response.Error = Error.ProgramError;
		}
		return Json(response);
	}


	[ServiceAdministratorApi]
	public IActionResult GetRouteStatisticsInfesWithRouterName(
	    [FromQuery] string? routerName,
	    [FromQuery] string? endPointName,
	    [FromQuery] long beginTimeStamp = 0,
	    [FromQuery] long endTimeStamp = 0,
	    [FromQuery] bool isGetRouterRouteStateStatisticsInfoInLastSecond = false)
	{
		var response = new GetRouteStatisticsInfesResponse();
		try
		{
			if (routerName != null)
			{
				var now = DateTime.Now;
				var beginTime = beginTimeStamp > 0
				    ? DateTimeUtil.DateTimeWithMillisecondsAfter1970(beginTimeStamp)
				    : now.AddSeconds(
				    -1 * Config.Router.RouteStatisticsInfoRetainSeconds);
				var endTime = endTimeStamp > 0
				    ? DateTimeUtil.DateTimeWithMillisecondsAfter1970(endTimeStamp)
				    : now;


				response.RouterName = routerName;
				response.EndPointName = endPointName;
				response.BeginTime = beginTime;
				response.EndTime = endTime;


				SystemResourceUsageMonitor.Shared.Current.ToGetRouteStatisticsInfesResponse(response);

				ThreadPool.GetMaxThreads(
				    out var workerThreadsCountMax,
				    out var completionPortThreadsCountMax);
				response.SystemWorkerThreadsCountMax = workerThreadsCountMax;
				response.SystemIOThreadsCountMax = completionPortThreadsCountMax;
				ThreadPool.GetAvailableThreads(
				    out var workerThreadsCount,
				    out var completionPortThreadsCount);
				response.SystemWorkerThreadsCount = workerThreadsCountMax - workerThreadsCount;
				response.SystemIOThreadsCount = completionPortThreadsCountMax - completionPortThreadsCount;

				response.SystemThreadTasksCountWaitingToWork = (int)ThreadPool.PendingWorkItemCount;
				response.SystemThreadsCountInPool = ThreadPool.ThreadCount;


				if (beginTime < endTime)
				{
					response.RouterStateStatisticsInfoInLastSecond
					    = Router.GetRouterStateStatisticsInfoInSecond(
					    routerName,
					    endTime,
					    isGetRouterRouteStateStatisticsInfoInLastSecond);
					response.RouteStatisticsInfes
					    = Router.GetRouteStatisticsInfesWithRouterName(
					    routerName,
					    endPointName,
					    beginTime,
					    endTime);
				}
				else
				{
					response.Error = Error.InvalidRequest;
				}
			}
			else
			{
				response.Error = Error.InvalidRequest;
			}
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(this, "获取路由统计信息失败，程序异常。", exception);
			//
			response.Error = Error.ProgramError;
		}
		return Json(response);
	}

	/// <summary>
	/// 查询终点返回500的请求，时间戳单位为毫秒，按请求开始时间筛选。
	/// </summary>
	[ServiceAdministratorApi]
	public IActionResult GetExceptionResponseList([FromQuery] string? routerName, [FromQuery] string? endPointName,
		[FromQuery] long beginTimeStamp = 0, [FromQuery] long endTimeStamp = 0)
	{
		var response = new GetExceptionResponseListResponse()
		{
			RouterName = routerName,
			EndPointName = endPointName
		};
		try
		{
			if (string.IsNullOrWhiteSpace(routerName) || beginTimeStamp < 0 || endTimeStamp < 0
				|| beginTimeStamp > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds() || endTimeStamp > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
			{
				response.Error = Error.InvalidRequest;
				return Json(response);
			}

			var now = DateTime.Now;
			var beginTime = beginTimeStamp > 0
				? DateTimeUtil.DateTimeWithMillisecondsAfter1970(beginTimeStamp)
				: now.AddSeconds(-1 * Config.Router.RouteStatisticsInfoRetainSeconds);
			var endTime = endTimeStamp > 0
				? DateTimeUtil.DateTimeWithMillisecondsAfter1970(endTimeStamp)
				: now;
			response.BeginTime = beginTime;
			response.EndTime = endTime;
			if (beginTime >= endTime)
			{
				response.Error = Error.InvalidRequest;
			}
			else
			{
				response.ExceptionResponseList = Router.GetExceptionResponseList(routerName, endPointName, beginTime, endTime);
			}
		}
		catch (ArgumentOutOfRangeException)
		{
			response.Error = Error.InvalidRequest;
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(this, "获取路由异常响应列表失败，程序异常。", exception);
			response.Error = Error.ProgramError;
		}
		return Json(response);
	}

	[ServiceAdministratorApi]
	public IActionResult GetRouteResponseDurationRankList([FromQuery] int pageIndex, [FromQuery] int pageSize)
	{
		var response = new GetRouteResponseDurationRankListResponse()
		{
			PageIndex = pageIndex,
			PageSize = pageSize
		};
		try
		{
			if (Router.ResponseDurationRankList != null)
			{
				response.AllRouteStatisticsInfoRecordersCount = Router.ResponseDurationRankList.Length;
				response.RouteStatisticsInfoRecorders = Router.ResponseDurationRankList?.Skip(pageSize * pageIndex).Take(pageSize);
			}
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(this, "获取路由响应时长排行榜失败，程序异常。", exception);
			//
			response.Error = Error.ProgramError;
		}
		return Json(response);
	}

	#endregion
}
