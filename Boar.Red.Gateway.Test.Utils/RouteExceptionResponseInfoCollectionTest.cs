using BaoXia.Constants;
using Boar.Red.Gateway.Controllers;
using Boar.Red.Gateway.Models;
using Boar.Red.Gateway.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test.Utils;

[TestClass]
public class RouteExceptionResponseInfoCollectionTest
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[TestMethod]
	public void Collect_OnlyRetainsLiveEndPoint500Responses()
	{
		var collection = new RouteExceptionResponseInfoCollection();
		var beginTime = DateTime.Today;
		var routeTime = beginTime.AddSeconds(1);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", routeTime), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", routeTime, 200), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", routeTime, 502), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", routeTime, isResponseGetFromEndPoint: false), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", routeTime, type: RouteStatisticsInfoRecorder.RecorderType.RequestRecorder), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", beginTime), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("", "X", routeTime), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "", routeTime), beginTime);

		var result = collection.GetExceptionResponseList("A", "X", beginTime, routeTime, beginTime);
		Assert.HasCount(1, result);
		Assert.AreEqual(500, result[0].ResponseHttpStatusCode);
		Assert.AreEqual("http://localhost/A/X", result[0].EndPointRequestUrl);
	}

	[TestMethod]
	public void GetExceptionResponseList_FiltersRouteEndPointAndInclusiveTimeRange()
	{
		var collection = new RouteExceptionResponseInfoCollection();
		var beginTime = DateTime.Today;
		var requestBeginTime = beginTime.AddSeconds(1);
		var requestEndTime = beginTime.AddSeconds(2);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", requestBeginTime), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", requestEndTime), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", requestEndTime.AddMilliseconds(1)), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "Y", requestBeginTime), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("B", "X", requestBeginTime), beginTime);

		Assert.HasCount(2, collection.GetExceptionResponseList("A", "X", requestBeginTime, requestEndTime, beginTime));
		Assert.HasCount(3, collection.GetExceptionResponseList("A", "All", requestBeginTime, requestEndTime, beginTime));
		Assert.HasCount(3, collection.GetExceptionResponseList("All", "X", requestBeginTime, requestEndTime, beginTime));
		Assert.HasCount(4, collection.GetExceptionResponseList("All", null, requestBeginTime, requestEndTime, beginTime));
		Assert.IsEmpty(collection.GetExceptionResponseList("missing", "X", requestBeginTime, requestEndTime, beginTime));
		Assert.IsEmpty(collection.GetExceptionResponseList("A", "X", requestEndTime, requestBeginTime, beginTime));
	}

	[TestMethod]
	public void RemoveExpired_HandlesOutOfOrderResponsesAndPartialSecondBoundary()
	{
		var collection = new RouteExceptionResponseInfoCollection();
		var beginTime = DateTime.Today;
		var expirationTime = beginTime.AddMilliseconds(2500);
		var retainedTime = expirationTime.AddMilliseconds(1);
		// 响应完成顺序与请求开始顺序可能不同。
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", retainedTime), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", beginTime.AddMilliseconds(2100)), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", beginTime.AddSeconds(1)), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", expirationTime), beginTime);
		collection.RemoveExpired(expirationTime);

		var result = collection.GetExceptionResponseList("A", "X", beginTime, beginTime.AddSeconds(5), beginTime);
		Assert.HasCount(1, result);
		Assert.AreEqual(retainedTime, result[0].RouteTime);

		collection.RemoveExpired(retainedTime);
		Assert.IsEmpty(collection.GetExceptionResponseList("A", "X", beginTime, beginTime.AddSeconds(5), beginTime));
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", retainedTime.AddSeconds(1)), retainedTime);
		Assert.HasCount(1, collection.GetExceptionResponseList("A", "X", beginTime, beginTime.AddSeconds(5), retainedTime));
	}

	[TestMethod]
	public void GetExceptionResponseList_ExcludesExpiredRecordsBeforeNextCleanup()
	{
		var collection = new RouteExceptionResponseInfoCollection();
		var beginTime = DateTime.Today;
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", beginTime.AddSeconds(1)), beginTime);
		collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", beginTime.AddSeconds(2)), beginTime);

		var result = collection.GetExceptionResponseList("A", "X", beginTime, beginTime.AddSeconds(3), beginTime.AddSeconds(1));
		Assert.HasCount(1, result);
		Assert.AreEqual(beginTime.AddSeconds(2), result[0].RouteTime);
	}

	[TestMethod]
	public void Recorder_PreservesRejectedStatusAndResetsForNextAttempt()
	{
		using var router = new Router(new RouteRule() { Name = "A" });
		using var recorder = new RouteStatisticsInfoRecorder(RouteStatisticsInfoRecorder.RecorderType.ResponseRecorder);
		recorder.IsResponseGetFromEndPoint = true;
		// 兜底缓存返回200，终点实际返回500。
		recorder.EndCreateResponseCache(200);
		recorder.EndResponse(router, new EndPointRequestInfo() { EndPointName = "X" }, true, 500);
		using var snapshot = new RouteStatisticsInfoRecorder(recorder);
		recorder.RestartWatchForCurrentStatisticsSection();

		Assert.AreEqual(500, snapshot.ResponseHttpStatusCode);
		Assert.AreEqual(0, recorder.ResponseHttpStatusCode);
		Assert.IsFalse(recorder.IsResponseGetFromEndPoint);
		recorder.EndCreateResponseCache(200);
		Assert.AreEqual(200, recorder.ResponseHttpStatusCode);
	}

	[TestMethod]
	public async Task GetExceptionResponseList_AllowsConcurrentQueriesDuringCollectionAndCleanupAsync()
	{
		var collection = new RouteExceptionResponseInfoCollection();
		var beginTime = DateTime.Today;
		var routeTime = beginTime.AddSeconds(1);
		var collector = Task.Run(() =>
		{
			for (var index = 0; index < 10000; index++)
			{
				collection.Collect(new RouteStatisticsInfoRecorderForTest("A", "X", routeTime), beginTime);
				if (index % 100 == 0)
				{
					collection.RemoveExpired(beginTime);
				}
			}
		});
		var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
		{
			for (var index = 0; index < 100; index++)
			{
				var result = collection.GetExceptionResponseList("A", "X", beginTime, routeTime.AddSeconds(1), beginTime);
				Assert.IsTrue(result.All(recorder => recorder.ResponseHttpStatusCode == 500));
			}
		})).ToArray();
		await Task.WhenAll(readers.Append(collector));

		Assert.HasCount(10000, collection.GetExceptionResponseList("A", "X", beginTime, routeTime.AddSeconds(1), beginTime));
		collection.RemoveExpired(routeTime);
		Assert.IsEmpty(collection.GetExceptionResponseList("A", "X", beginTime, routeTime.AddSeconds(1), beginTime));
	}

	[TestMethod]
	public void Controller_RejectsInvalidParametersAndReturnsEmptyListForUnknownRoute()
	{
		var controller = new RouteStatisticsController();
		foreach (var result in new[]
		{
			controller.GetExceptionResponseList(null, null),
			controller.GetExceptionResponseList("A", "X", -1),
			controller.GetExceptionResponseList("A", "X", 2, 1),
			controller.GetExceptionResponseList("A", "X", long.MaxValue)
		})
		{
			var response = (GetExceptionResponseListResponse)((JsonResult)result).Value!;
			Assert.AreEqual(Error.InvalidRequest, response.Error);
		}
		var emptyResult = (JsonResult)controller.GetExceptionResponseList("missing", "X");
		var emptyResponse = (GetExceptionResponseListResponse)emptyResult.Value!;
		Assert.IsEmpty(emptyResponse.ExceptionResponseList);
	}

	#endregion
}
