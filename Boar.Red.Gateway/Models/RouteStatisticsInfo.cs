using BaoXia.Utils;
using System;
using System.ComponentModel.DataAnnotations;

namespace Boar.Red.Gateway.Models;

public class RouteStatisticsInfo
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性


	[Key]
	public int Id { get; set; }

	public string? Name { get; set; }

	public string? RouterName { get; set; }

	public string? EndPointName { get; set; }

	public DateTime RouteTimeInSecond { get; set; }

	public int RequestsCount { get; set; }

	public float RequestsCountRate { get; set; }

	public int RequestsCountFailed { get; set; }

	public float RequestsCountFailedRate { get; set; }

	public int RequestsCountToEndPoint { get; set; }

	public float RequestsCountToEndPointRate { get; set; }

	public RouteStatisticsTimeSpan TimeSpanToResponse { get; set; } = new();

	public RouteStatisticsTimeSpan TimeSpanToMatchRouteRule { get; set; } = new();

	public RouteStatisticsTimeSpan TimeSpanToCreateRequestHashCode { get; set; } = new();

	public RouteStatisticsTimeSpan TimeSpanToFindResponseCache { get; set; } = new();

	public RouteStatisticsTimeSpan TimeSpanToGetOriginalResponseCache { get; set; } = new();

	public RouteStatisticsTimeSpan TimeSpanToCreateResponseCache { get; set; } = new();

	public RouteStatisticsTimeSpan TimeSpanToSendResponse { get; set; } = new();

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public RouteStatisticsInfo(
	    string name,
	    string? routerName,
	    string? endPointName,
	    DateTime routeTimeInSecond)
	{
		this.Name = name;

		this.RouterName = routerName;
		this.EndPointName = endPointName;

		this.RouteTimeInSecond = routeTimeInSecond;
	}

	public RouteStatisticsInfo(RouteStatisticsInfo anotherRouteStatisticsInfo)
	{
		this.Id = anotherRouteStatisticsInfo.Id;

		this.Name = anotherRouteStatisticsInfo.Name;

		this.RouterName = anotherRouteStatisticsInfo.RouterName;

		this.EndPointName = anotherRouteStatisticsInfo.EndPointName;

		this.RouteTimeInSecond = anotherRouteStatisticsInfo.RouteTimeInSecond;

		this.RequestsCount = anotherRouteStatisticsInfo.RequestsCount;

		this.RequestsCountRate = anotherRouteStatisticsInfo.RequestsCountRate;

		this.RequestsCountFailed = anotherRouteStatisticsInfo.RequestsCountFailed;

		this.RequestsCountFailedRate = anotherRouteStatisticsInfo.RequestsCountFailedRate;

		this.RequestsCountToEndPoint = anotherRouteStatisticsInfo.RequestsCountToEndPoint;

		this.RequestsCountToEndPointRate = anotherRouteStatisticsInfo.RequestsCountToEndPointRate;

		this.TimeSpanToResponse = anotherRouteStatisticsInfo.TimeSpanToResponse;

		this.TimeSpanToMatchRouteRule = anotherRouteStatisticsInfo.TimeSpanToMatchRouteRule;

		this.TimeSpanToCreateRequestHashCode = anotherRouteStatisticsInfo.TimeSpanToCreateRequestHashCode;

		this.TimeSpanToFindResponseCache = anotherRouteStatisticsInfo.TimeSpanToFindResponseCache;

		this.TimeSpanToGetOriginalResponseCache = anotherRouteStatisticsInfo.TimeSpanToGetOriginalResponseCache;

		this.TimeSpanToCreateResponseCache = anotherRouteStatisticsInfo.TimeSpanToCreateResponseCache;

		this.TimeSpanToSendResponse = anotherRouteStatisticsInfo.TimeSpanToSendResponse;
	}

	public RouteStatisticsInfo AddStatisticsInfoWithRecorder(
	    RouteStatisticsInfoRecorder statisticsInfoRecorder)
	{
		if (statisticsInfoRecorder == null)
		{
			return this;
		}

		////////////////////////////////////////////////
		// RequestsCount&RequestsCountFailed
		////////////////////////////////////////////////
		if (statisticsInfoRecorder.Type
		    == RouteStatisticsInfoRecorder.RecorderType.RequestRecorder)
		{
			this.RequestsCount++;
			return this;
		}

		////////////////////////////////////////////////
		// RequestsCount&RequestsCountFailed
		////////////////////////////////////////////////
		// !!!⚠ 错误信息应当和终点响应信息一样聚合 ⚠!!!
		//{
		//        if (statisticsInfoRecorder.IsRouteFailed)
		//        {
		//                this.RequestsCountFailed++;
		//        }
		//}

		////////////////////////////////////////////////
		// TimeSpanToResponse
		////////////////////////////////////////////////
		{
			var timeSpanToResponse = this.TimeSpanToResponse;
			{
				// !!!
				timeSpanToResponse.AddMilliseconds(statisticsInfoRecorder.MillisecondsToResponse);
				// !!!
			}
			this.TimeSpanToResponse = timeSpanToResponse;
		}

		////////////////////////////////////////////////
		// TimeSpanToMatchRouteRule
		////////////////////////////////////////////////
		{
			var timeSpanToMatchRouteRule = this.TimeSpanToMatchRouteRule;
			{
				// !!!
				timeSpanToMatchRouteRule.AddMilliseconds(statisticsInfoRecorder.MillisecondsToMatchRouteRule);
				// !!!
			}
			this.TimeSpanToMatchRouteRule = timeSpanToMatchRouteRule;
		}

		////////////////////////////////////////////////
		// TimeSpanToCreateRequestHashCode
		////////////////////////////////////////////////
		{
			var timeSpanToCreateRequestHashCode = this.TimeSpanToCreateRequestHashCode;
			{
				// !!!
				timeSpanToCreateRequestHashCode.AddMilliseconds(statisticsInfoRecorder.MillisecondsToCreateRequestHashCode);
				// !!!
			}
			this.TimeSpanToCreateRequestHashCode = timeSpanToCreateRequestHashCode;
		}

		////////////////////////////////////////////////
		// TimeSpanToFindResponseCache
		////////////////////////////////////////////////
		{
			var timeSpanToFindResponseCache = this.TimeSpanToFindResponseCache;
			{
				// !!!
				timeSpanToFindResponseCache.AddMilliseconds(statisticsInfoRecorder.MillisecondsToFindResponseCache);
				// !!!
			}
			this.TimeSpanToFindResponseCache = timeSpanToFindResponseCache;
		}

		////////////////////////////////////////////////
		// TimeSpanToGetOriginalResponseCache
		////////////////////////////////////////////////
		{
			var timeSpanToGetOriginalResponseCache = this.TimeSpanToGetOriginalResponseCache;
			{
				// !!!
				timeSpanToGetOriginalResponseCache.AddMilliseconds(statisticsInfoRecorder.MillisecondsToGetOriginalResponseCache);
				// !!!
			}
			this.TimeSpanToGetOriginalResponseCache = timeSpanToGetOriginalResponseCache;
		}

		////////////////////////////////////////////////
		// TimeSpanToCreateResponseCache
		////////////////////////////////////////////////
		{
			var timeSpanToCreateResponseCache = this.TimeSpanToCreateResponseCache;
			{
				// !!!
				timeSpanToCreateResponseCache.AddMilliseconds(statisticsInfoRecorder.MillisecondsToCreateResponseCache);
				// !!!
			}
			this.TimeSpanToCreateResponseCache = timeSpanToCreateResponseCache;
		}

		////////////////////////////////////////////////
		// TimeSpanToSendResponse
		////////////////////////////////////////////////
		{
			var timeSpanToSendResponse = this.TimeSpanToSendResponse;
			{
				// !!!
				timeSpanToSendResponse.AddMilliseconds(statisticsInfoRecorder.MillisecondsToSendResponse);
				// !!!
			}
			this.TimeSpanToSendResponse = timeSpanToSendResponse;
		}

		return this;
	}

	public RouteStatisticsInfo AddStatisticsInfo(
	    RouteStatisticsInfo statisticsInfo)
	{
		if (statisticsInfo == null)
		{
			return this;
		}
		if (statisticsInfo == this)
		{
			throw new ArgumentNullException(
			    nameof(statisticsInfo),
			    "”statisticsInfo“为当前统计信息，不可叠加。");
		}

		////////////////////////////////////////////////
		// RequestsCount
		////////////////////////////////////////////////
		{
			this.RequestsCount += statisticsInfo.RequestsCount;
			this.RequestsCountFailed += statisticsInfo.RequestsCountFailed;
			this.RequestsCountToEndPoint += statisticsInfo.RequestsCountToEndPoint;
		}

		////////////////////////////////////////////////
		// TimeSpanToResponse
		////////////////////////////////////////////////
		{
			var timeSpanToResponse = this.TimeSpanToResponse;
			if (timeSpanToResponse == null)
			{
				timeSpanToResponse = statisticsInfo.TimeSpanToResponse;
			}
			else
			{
				// !!!
				timeSpanToResponse.AddStatisticsTimeSpan(statisticsInfo.TimeSpanToResponse);
				// !!!
			}
			this.TimeSpanToResponse = timeSpanToResponse;
		}

		////////////////////////////////////////////////
		// TimeSpanToMatchRouteRule
		////////////////////////////////////////////////
		{
			var timeSpanToMatchRouteRule = this.TimeSpanToMatchRouteRule;
			if (timeSpanToMatchRouteRule == null)
			{
				timeSpanToMatchRouteRule = statisticsInfo.TimeSpanToMatchRouteRule;
			}
			else
			{
				// !!!
				timeSpanToMatchRouteRule.AddStatisticsTimeSpan(statisticsInfo.TimeSpanToMatchRouteRule);
				// !!!
			}
			this.TimeSpanToMatchRouteRule = timeSpanToMatchRouteRule;
		}

		////////////////////////////////////////////////
		// TimeSpanToCreateRequestHashCode
		////////////////////////////////////////////////
		{
			var timeSpanToCreateRequestHashCode = this.TimeSpanToCreateRequestHashCode;
			if (timeSpanToCreateRequestHashCode == null)
			{
				timeSpanToCreateRequestHashCode = statisticsInfo.TimeSpanToCreateRequestHashCode;
			}
			else
			{
				// !!!
				timeSpanToCreateRequestHashCode.AddStatisticsTimeSpan(statisticsInfo.TimeSpanToCreateRequestHashCode);
				// !!!
			}
			this.TimeSpanToCreateRequestHashCode = timeSpanToCreateRequestHashCode;
		}

		////////////////////////////////////////////////
		// TimeSpanToFindResponseCache
		////////////////////////////////////////////////
		{
			var timeSpanToFindResponseCache = this.TimeSpanToFindResponseCache;
			if (timeSpanToFindResponseCache == null)
			{
				timeSpanToFindResponseCache = statisticsInfo.TimeSpanToFindResponseCache;
			}
			else
			{
				// !!!
				timeSpanToFindResponseCache.AddStatisticsTimeSpan(statisticsInfo.TimeSpanToFindResponseCache);
				// !!!
			}
			this.TimeSpanToFindResponseCache = timeSpanToFindResponseCache;
		}

		////////////////////////////////////////////////
		// TimeSpanToGetOriginalResponseCache
		////////////////////////////////////////////////
		{
			var timeSpanToGetOriginalResponseCache = this.TimeSpanToGetOriginalResponseCache;
			if (timeSpanToGetOriginalResponseCache == null)
			{
				timeSpanToGetOriginalResponseCache = statisticsInfo.TimeSpanToGetOriginalResponseCache;
			}
			else
			{
				// !!!
				timeSpanToGetOriginalResponseCache.AddStatisticsTimeSpan(statisticsInfo.TimeSpanToGetOriginalResponseCache);
				// !!!
			}
			this.TimeSpanToGetOriginalResponseCache = timeSpanToGetOriginalResponseCache;
		}

		////////////////////////////////////////////////
		// TimeSpanToCreateResponseCache
		////////////////////////////////////////////////
		{
			var timeSpanToCreateResponseCache = this.TimeSpanToCreateResponseCache;
			if (timeSpanToCreateResponseCache == null)
			{
				timeSpanToCreateResponseCache = statisticsInfo.TimeSpanToCreateResponseCache;
			}
			else
			{
				// !!!
				timeSpanToCreateResponseCache.AddStatisticsTimeSpan(statisticsInfo.TimeSpanToCreateResponseCache);
				// !!!
			}
			this.TimeSpanToCreateResponseCache = timeSpanToCreateResponseCache;
		}

		////////////////////////////////////////////////
		// TimeSpanToSendResponse
		////////////////////////////////////////////////
		{
			var timeSpanToSendResponse = this.TimeSpanToSendResponse;
			if (timeSpanToSendResponse == null)
			{
				timeSpanToSendResponse = statisticsInfo.TimeSpanToSendResponse;
			}
			else
			{
				// !!!
				timeSpanToSendResponse.AddStatisticsTimeSpan(statisticsInfo.TimeSpanToSendResponse);
				// !!!
			}
			this.TimeSpanToSendResponse = timeSpanToSendResponse;
		}

		return this;
	}


	#endregion


	////////////////////////////////////////////////
	// @重载
	////////////////////////////////////////////////

	#region 重载

	public override string ToString()
	{
		var jsonString = StringUtil.StringByJsonSerializeObject(this);
		{ }
		return jsonString;
	}

	#endregion
}