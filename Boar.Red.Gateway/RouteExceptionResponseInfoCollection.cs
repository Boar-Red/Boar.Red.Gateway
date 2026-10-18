using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

namespace Boar.Red.Gateway;

/// <summary>
/// 异常响应记录。收集和清理由路由统计后台任务串行执行，查询可以并发执行。
/// </summary>
public class RouteExceptionResponseInfoCollection
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	private readonly ConcurrentDictionary<(string RouterName, string EndPointName), SortedList<DateTime, List<RouteStatisticsInfoRecorder>>>
		_responseInfes = new();

	#endregion


	////////////////////////////////////////////////
	// @私有方法
	////////////////////////////////////////////////

	#region 私有方法

	private static DateTime GetRouteTimeInSecond(DateTime routeTime)
	{
		return new DateTime(routeTime.Ticks - routeTime.Ticks % TimeSpan.TicksPerSecond, routeTime.Kind);
	}

	private static void AppendResponseInfes(SortedList<DateTime, List<RouteStatisticsInfoRecorder>> responseInfes, DateTime beginTime, DateTime endTime,
		DateTime validRouteTimeMin, List<RouteStatisticsInfoRecorder> result)
	{
		lock (responseInfes)
		{
			// 二分定位开始秒，避免查询短时间段时扫描全部保留记录。
			var beginTimeInSecond = GetRouteTimeInSecond(beginTime > validRouteTimeMin ? beginTime : validRouteTimeMin);
			var lowerIndex = 0;
			var upperIndex = responseInfes.Count;
			while (lowerIndex < upperIndex)
			{
				var middleIndex = lowerIndex + (upperIndex - lowerIndex) / 2;
				if (responseInfes.Keys[middleIndex] < beginTimeInSecond)
				{
					lowerIndex = middleIndex + 1;
				}
				else
				{
					upperIndex = middleIndex;
				}
			}
			for (var bucketIndex = lowerIndex; bucketIndex < responseInfes.Count && responseInfes.Keys[bucketIndex] <= endTime; bucketIndex++)
			{
				foreach (var recorder in responseInfes.Values[bucketIndex])
				{
					if (recorder.RouteTime >= beginTime && recorder.RouteTime <= endTime && recorder.RouteTime > validRouteTimeMin)
					{
						result.Add(recorder);
					}
				}
			}
		}
	}

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public void Collect(RouteStatisticsInfoRecorder recorder, DateTime validRouteTimeMin)
	{
		if (recorder.Type != RouteStatisticsInfoRecorder.RecorderType.ResponseRecorder
			|| recorder.ResponseHttpStatusCode != (int)HttpStatusCode.InternalServerError
			|| !recorder.IsResponseGetFromEndPoint
			|| string.IsNullOrEmpty(recorder.RouterName)
			|| string.IsNullOrEmpty(recorder.EndPointName)
			|| recorder.RouteTime <= validRouteTimeMin)
		{
			return;
		}

		var responseInfes = _responseInfes.GetOrAdd((recorder.RouterName, recorder.EndPointName), static _ => new());
		var routeTimeInSecond = GetRouteTimeInSecond(recorder.RouteTime);
		lock (responseInfes)
		{
			if (!responseInfes.TryGetValue(routeTimeInSecond, out var bucket))
			{
				bucket = [];
				responseInfes.Add(routeTimeInSecond, bucket);
			}
			// 保留记录器引用，不复制请求信息或响应正文。
			bucket.Add(recorder);
		}
	}

	public void RemoveExpired(DateTime validRouteTimeMin)
	{
		var validRouteTimeMinInSecond = GetRouteTimeInSecond(validRouteTimeMin);
		foreach (var responseInfesInfo in _responseInfes)
		{
			var responseInfes = responseInfesInfo.Value;
			lock (responseInfes)
			{
				// 完整过期的秒桶直接移除，只检查保留时间边界所在秒的请求。
				while (responseInfes.Count > 0 && responseInfes.Keys[0] < validRouteTimeMinInSecond)
				{
					responseInfes.RemoveAt(0);
				}
				if (responseInfes.Count > 0 && responseInfes.Keys[0] == validRouteTimeMinInSecond)
				{
					var bucket = responseInfes.Values[0];
					bucket.RemoveAll(recorder => recorder.RouteTime <= validRouteTimeMin);
					if (bucket.Count == 0)
					{
						responseInfes.RemoveAt(0);
					}
				}
				if (responseInfes.Count == 0)
				{
					_responseInfes.TryRemove(responseInfesInfo.Key, out _);
				}
			}
		}
	}

	/// <summary>
	/// 按请求开始时间查询，时间区间包含两端；All表示全部路由或全部终点。
	/// </summary>
	public List<RouteStatisticsInfoRecorder> GetExceptionResponseList(string routerName, string? endPointName, DateTime beginTime, DateTime endTime, DateTime validRouteTimeMin)
	{
		List<RouteStatisticsInfoRecorder> result = [];
		if (string.IsNullOrWhiteSpace(routerName) || beginTime >= endTime || endTime <= validRouteTimeMin)
		{
			return result;
		}

		var isAllRouters = string.Equals(routerName, Router.RouterName_All, StringComparison.OrdinalIgnoreCase);
		var isAllEndPoints = string.IsNullOrEmpty(endPointName) || string.Equals(endPointName, Router.EndPointName_All, StringComparison.OrdinalIgnoreCase);
		if (!isAllRouters && !isAllEndPoints)
		{
			if (_responseInfes.TryGetValue((routerName, endPointName!), out var responseInfes))
			{
				AppendResponseInfes(responseInfes, beginTime, endTime, validRouteTimeMin, result);
			}
		}
		else
		{
			foreach (var responseInfesInfo in _responseInfes)
			{
				if ((isAllRouters || responseInfesInfo.Key.RouterName == routerName)
					&& (isAllEndPoints || responseInfesInfo.Key.EndPointName == endPointName))
				{
					AppendResponseInfes(responseInfesInfo.Value, beginTime, endTime, validRouteTimeMin, result);
				}
			}
		}
		// 查询结果脱离集合锁后排序，不阻塞后台收集。
		result.Sort(static (recorderA, recorderB) => recorderA.RouteTime.CompareTo(recorderB.RouteTime));
		return result;
	}

	#endregion
}
