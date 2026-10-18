using System;

namespace Boar.Red.Gateway.Test.Utils;

public class RouteStatisticsInfoRecorderForTest : RouteStatisticsInfoRecorder
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public RouteStatisticsInfoRecorderForTest(string routerName, string endPointName, DateTime routeTime, int statusCode = 500,
		bool isResponseGetFromEndPoint = true, RecorderType type = RecorderType.ResponseRecorder) : base(type)
	{
		_routerName = routerName;
		_endPointName = endPointName;
		_endPointRequestUrl = "http://localhost/" + routerName + "/" + endPointName;
		_routeTime = routeTime;
		_responseTime = routeTime.AddMilliseconds(100);
		_responseHttpStatusCode = statusCode;
		this.IsResponseGetFromEndPoint = isResponseGetFromEndPoint;
	}

	#endregion
}
