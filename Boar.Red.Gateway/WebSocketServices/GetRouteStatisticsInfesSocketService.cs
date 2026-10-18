using BaoXia.Constants;
using BaoXia.Utils;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Extensions;
using Boar.Red.Gateway.LogFiles;
using Boar.Red.Gateway.Utils;
using Boar.Red.Gateway.ViewModels;
using Boar.Red.Service.Attributes;
using Boar.Red.Service.BrService.Services;
using Boar.Red.Service.BrService.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.WebSocketServices;

[ServiceAdministratorApi]
public class GetRouteStatisticsInfesSocketService() : WebSocketService<GetRouteStatisticsInfesRequest, GetRouteStatisticsInfesResponse>(
	"/routeStatistics/getRouteStatisticsInfes")
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public GetRouteStatisticsInfesResponse CreateCurrentRouteStatisticsInfesResponse(GetRouteStatisticsInfesRequest? currentRequestParam)
	{
		var response = new GetRouteStatisticsInfesResponse();
		try
		{
			var routerName = currentRequestParam?.RouterName;
			var beginTimeStamp = currentRequestParam?.BeginTimeStamp ?? 0;
			var endTimeStamp = currentRequestParam?.EndTimeStamp ?? 0;
			var endPointName = currentRequestParam?.EndPointName;
			var isGetRouterRouteStateStatisticsInfoInLastSecond
				= currentRequestParam?.IsGetRouterRouteStateStatisticsInfoInLastSecond ?? false;

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
		return response;
	}

	#endregion


	////////////////////////////////////////////////
	// @实现“WebSocketService”
	////////////////////////////////////////////////

	#region 实现“WebSocketService”

	protected override async Task<WebSocketServiceResponseInfo<GetRouteStatisticsInfesResponse>?> DidProcessRequestAsync(
		GetRouteStatisticsInfesRequest? requestParam)
	{
		var response = CreateCurrentRouteStatisticsInfesResponse(requestParam);
		return await Task.FromResult(new WebSocketServiceResponseInfo<GetRouteStatisticsInfesResponse>()
		{
			ResponseParam = response
		});
	}

	#endregion
}
