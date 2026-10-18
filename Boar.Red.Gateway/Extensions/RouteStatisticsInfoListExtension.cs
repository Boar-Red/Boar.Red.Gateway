using BaoXia.Utils.Extensions;
using Boar.Red.Gateway.Models;
using System;
using System.Collections.Generic;

namespace Boar.Red.Gateway.Extensions
{
	public static class RouteStatisticsInfoListExtension
	{
		public static List<RouteStatisticsInfo> MergeTo(
		    this List<RouteStatisticsInfo> statisticsInfoList,
		    List<RouteStatisticsInfo> anotherStatisticsInfoList,
		    DateTime routeStatisticsInfoValidRouteTimeMin,
		    Action<RouteStatisticsInfo> toDidStatisticsInfoUpdated)
		{
			if (statisticsInfoList == null
			    || statisticsInfoList.Count < 1)
			{
				return anotherStatisticsInfoList;
			}
			if (statisticsInfoList == anotherStatisticsInfoList)
			{
				return anotherStatisticsInfoList;
			}

			if (anotherStatisticsInfoList == null)
			{
				anotherStatisticsInfoList = new List<RouteStatisticsInfo>();
			}
			if (anotherStatisticsInfoList.Count > 0)
			{
				var newRouteStatisticsInfoList = new List<RouteStatisticsInfo>();
				foreach (var anotherStatisticsInfo in anotherStatisticsInfoList)
				{
					if (anotherStatisticsInfo.RouteTimeInSecond
					>= routeStatisticsInfoValidRouteTimeMin)
					{
						newRouteStatisticsInfoList.Add(anotherStatisticsInfo);
					}
				}
				// !!!
				anotherStatisticsInfoList.Clear();
				anotherStatisticsInfoList.AddRange(newRouteStatisticsInfoList);
				// !!!
			}

			foreach (var statisticsInfo in statisticsInfoList)
			{
				RouteStatisticsInfo? routeStatisticsInfoUpdatedExisted = null;
				var newRouteStatisticsInfoRouteTime = statisticsInfo.RouteTimeInSecond;
				foreach (var anotherStatisticsInfo in anotherStatisticsInfoList)
				{
					var anotherStatisticsInfoRouteTime
					= anotherStatisticsInfo.RouteTimeInSecond;
					if (anotherStatisticsInfoRouteTime.EqualsInSecond(newRouteStatisticsInfoRouteTime))
					{
						// !!!
						routeStatisticsInfoUpdatedExisted = anotherStatisticsInfo;
						// !!!
						break;
					}
				}
				RouteStatisticsInfo routeStatisticsInfoUpdated;
				if (routeStatisticsInfoUpdatedExisted == null)
				{
					// !!!
					routeStatisticsInfoUpdated = new RouteStatisticsInfo(statisticsInfo);
					// !!!
					anotherStatisticsInfoList.Add(routeStatisticsInfoUpdated);
				}
				else
				{
					routeStatisticsInfoUpdated = routeStatisticsInfoUpdatedExisted;
					routeStatisticsInfoUpdated.AddStatisticsInfo(statisticsInfo);
				}

				////////////////////////////////////////////////
				// !!! 统计信息事件节点 !!!
				////////////////////////////////////////////////
				{
					toDidStatisticsInfoUpdated?.Invoke(routeStatisticsInfoUpdated);
				}
			}

			// !!!
			anotherStatisticsInfoList.Sort(
			(statisticsInfoA, statisticsInfoB) =>
			{
				return statisticsInfoA.RouteTimeInSecond.CompareTo(
		statisticsInfoB.RouteTimeInSecond);
			});
			// !!!
			return anotherStatisticsInfoList;
		}

		public static List<RouteStatisticsInfo> MergeFrom(
		    this List<RouteStatisticsInfo> statisticsInfoList,
		    List<RouteStatisticsInfo> anotherStatisticsInfoList,
		    DateTime routeStatisticsInfoValidRouteTimeMin,
		    Action<RouteStatisticsInfo> toDidStatisticsInfoUpdated)
		{
			return anotherStatisticsInfoList.MergeTo(
			    statisticsInfoList,
			    routeStatisticsInfoValidRouteTimeMin,
			    toDidStatisticsInfoUpdated);
		}

		public static List<RouteStatisticsInfo> SortWithRouteTimeASC(
		    this List<RouteStatisticsInfo> statisticsInfoList)
		{
			statisticsInfoList.Sort((statisticsInfoA, statisticsInfoB) =>
			{
				return statisticsInfoA.RouteTimeInSecond.CompareTo(
		statisticsInfoB.RouteTimeInSecond);
			});
			return statisticsInfoList;
		}
	}
}
