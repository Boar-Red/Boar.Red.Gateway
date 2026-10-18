using BaoXia.Utils.Extensions;
using Boar.Red.Gateway.Models;
using System;
using System.Collections.Generic;

namespace Boar.Red.Gateway.Extensions
{
	public static class RouteStatisticsInfoRecorderListExtension
	{
		public static List<List<RouteStatisticsInfo>>? ToRouteStatisticsInfoLists(
		    this List<RouteStatisticsInfoRecorder> originalRecorders,
		    Func<RouteStatisticsInfoRecorder, string> toGetRouteStatisticsInfoName,
		    Func<RouteStatisticsInfoRecorder, RouteStatisticsInfoRecorder, bool> toIsRecordersInSameRouteSatisticsInfoList)
		{
			if (originalRecorders == null
			    || originalRecorders.Count < 1
			    || toGetRouteStatisticsInfoName == null
			    || toIsRecordersInSameRouteSatisticsInfoList == null)
			{
				return null;
			}

			// !!!
			var recorderList = new List<RouteStatisticsInfoRecorder>(originalRecorders);
			// !!!

			var currentRouteStatisticsInfoLists = new List<List<RouteStatisticsInfo>>();
			var currentRouteStatisticsInfoListRecorders = new List<RouteStatisticsInfoRecorder>();

			for (var currentRecorderIndex = 0;
			currentRecorderIndex < recorderList.Count;
			currentRecorderIndex++)
			{
				var currentRecorder = recorderList[currentRecorderIndex];
				var currentRouteStatisticsInfoName = toGetRouteStatisticsInfoName(currentRecorder);
				// !!!
				currentRouteStatisticsInfoListRecorders.Clear();
				// !!!

				////////////////////////////////////////////////
				// 1/3，计算请求从【客户端】到【路由】间的统计数据：
				////////////////////////////////////////////////
				var currentRouteStatisticsInfo = new RouteStatisticsInfo(
				       currentRouteStatisticsInfoName,
				       //
				       currentRecorder.RouterName,
				       currentRecorder.EndPointName,
				       //
				       currentRecorder.RouteTime);
				{
					currentRouteStatisticsInfo.AddStatisticsInfoWithRecorder(currentRecorder);
					currentRouteStatisticsInfoListRecorders.Add(currentRecorder);
				}
				// !!!
				var currentRouteStatisticsInfoList = new List<RouteStatisticsInfo>();
				{
					currentRouteStatisticsInfoList.Add(currentRouteStatisticsInfo);
				}
				// !!!
				currentRouteStatisticsInfoLists.Add(currentRouteStatisticsInfoList);
				// !!!

				for (var otherRecorderIndex = currentRecorderIndex + 1;
				otherRecorderIndex < recorderList.Count;
				otherRecorderIndex++)
				{
					var otherRecorder = recorderList[otherRecorderIndex];
					var isRecordersInSameRouteSatisticsInfoList
					    = toIsRecordersInSameRouteSatisticsInfoList(
					    otherRecorder,
					    currentRecorder);
					if (isRecordersInSameRouteSatisticsInfoList)
					{
						var otherRecorderRouteTime = otherRecorder.RouteTime;
						if (otherRecorderRouteTime != default)
						{
							RouteStatisticsInfo? routeStatisticsInfoInSameSecond = null;
							foreach (var routeStatisticsInfoExisted in currentRouteStatisticsInfoList)
							{
								if (otherRecorderRouteTime.EqualsInSecond(
								    routeStatisticsInfoExisted.RouteTimeInSecond))
								{
									// !!!
									routeStatisticsInfoInSameSecond = routeStatisticsInfoExisted;
									// !!!
									break;
								}
							}

							if (routeStatisticsInfoInSameSecond == null)
							{
								routeStatisticsInfoInSameSecond = new RouteStatisticsInfo(
								    currentRouteStatisticsInfoName,
								       //
								       otherRecorder.RouterName,
								       otherRecorder.EndPointName,
								       //
								       otherRecorder.RouteTime);
								// !!!
								currentRouteStatisticsInfoList.Add(routeStatisticsInfoInSameSecond);
								// !!!
							}
							// !!!
							routeStatisticsInfoInSameSecond.AddStatisticsInfoWithRecorder(otherRecorder);
							currentRouteStatisticsInfoListRecorders.Add(otherRecorder);
							// !!!
						}

						////////////////////////////////////////////////
						// !!!
						recorderList.RemoveAt(otherRecorderIndex);
						otherRecorderIndex--;
						// !!!
						////////////////////////////////////////////////
					}
				}
				////////////////////////////////////////////////
				// !!!
				recorderList.RemoveAt(currentRecorderIndex);
				currentRecorderIndex--;
				// !!!
				////////////////////////////////////////////////

				////////////////////////////////////////////////
				// 2/3，计算请求从【路由】到【终点】间的统计数据：
				////////////////////////////////////////////////
				foreach (var recorder in currentRouteStatisticsInfoListRecorders)
				{
					if (recorder.Type == RouteStatisticsInfoRecorder.RecorderType.ResponseRecorder)
					{
						var recorderEndPointRequestTime = recorder.EndPointRequestTime;
						if (recorderEndPointRequestTime != default)
						{
							RouteStatisticsInfo? routeStatisticsInfoInSameSecondExisted = null;
							foreach (var routeStatisticsInfoExisted in currentRouteStatisticsInfoList)
							{
								if (recorderEndPointRequestTime.EqualsInSecond(
								    routeStatisticsInfoExisted.RouteTimeInSecond))
								{
									// !!!
									routeStatisticsInfoInSameSecondExisted = routeStatisticsInfoExisted;
									// !!!
									break;
								}
							}

							RouteStatisticsInfo routeStatisticsInfoInSameSecond;
							if (routeStatisticsInfoInSameSecondExisted != null)
							{
								routeStatisticsInfoInSameSecond = routeStatisticsInfoInSameSecondExisted;
							}
							else
							{
								routeStatisticsInfoInSameSecond = new RouteStatisticsInfo(
								    currentRouteStatisticsInfoName,
								       //
								       recorder.RouterName,
								       recorder.EndPointName,
								       //
								       recorderEndPointRequestTime);
								// !!!
								currentRouteStatisticsInfoList.Add(routeStatisticsInfoInSameSecond);
								// !!!
							}
							// !!!
							routeStatisticsInfoInSameSecond.RequestsCountToEndPoint++;
							if (recorder.IsRouteFailed)
							{
								routeStatisticsInfoInSameSecond.RequestsCountFailed++;
							}
							// !!!
						}
					}
				}

				////////////////////////////////////////////////
				// 3/3，按请求时间进行排序：
				////////////////////////////////////////////////
				{
					currentRouteStatisticsInfoList.SortWithRouteTimeASC();
				}
			}
			return currentRouteStatisticsInfoLists;
		}
	}
}
