using Boar.Red.Gateway.Models;
using System;
using System.Diagnostics;

namespace Boar.Red.Gateway;

public class RouteStatisticsInfoRecorder : IDisposable
{
	////////////////////////////////////////////////
	// @静态常量
	////////////////////////////////////////////////

	#region 静态常量

	public enum RecorderType
	{
		RequestRecorder,
		ResponseRecorder
	}

	#endregion


	////////////////////////////////////////////////
	// @类方法
	////////////////////////////////////////////////

	#region 类方法

	public static string CreateRouteStatisticsInfoNameWithRouterName(string? routerName, string? endPointName)
	{
		var routeStatisticsInfoName = routerName ?? string.Empty;
		if (endPointName?.Length > 0)
		{
			routeStatisticsInfoName += "->" + routerName;
		}
		return routeStatisticsInfoName;
	}

	#endregion


	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性


	protected RecorderType _type;

	public RecorderType Type => _type;



	protected string? _routerName;

	public string? RouterName => _routerName;



	protected DateTime _routeTime;

	public DateTime RouteTime => _routeTime;



	protected string? _endPointName;

	public string? EndPointName => _endPointName;


	protected string? _endPointRequestUrl;

	public string? EndPointRequestUrl => _endPointRequestUrl;


	protected DateTime _endPointRequestTime;
	public DateTime EndPointRequestTime => _endPointRequestTime;


	protected DateTime _responseTime;

	public DateTime ResponseTime => _responseTime;


	protected int _responseHttpStatusCode;

	/// <summary>
	/// 本次线路的响应Http状态码，未取得响应时为0。
	/// </summary>
	public int ResponseHttpStatusCode => _responseHttpStatusCode;


	protected bool _isRouteFailed;
	public bool IsRouteFailed => _isRouteFailed;

	/// <summary>
	/// 是否已检查过响应时长。
	/// </summary>
	public bool IsResponseDurationChecked { get; set; }

	////////////////////////////////////////////////


	protected Stopwatch _performanceWatch = new();

	public long MillisecondsToResponse
	{
		get
		{
			var millsecondsToResponse
			    = this.MillisecondsToMatchRouteRule
			    + this.MillisecondsToWaitingForRPSMax
			    + this.MillisecondsToCreateRequestHashCode
			    + this.MillisecondsToFindResponseCache
			    + this.MillisecondsToGetOriginalResponseCache
			    + this.MillisecondsToCreateResponseCache
			    + this.MillisecondsToSendResponse;
			{ }
			return millsecondsToResponse;
		}
	}

	public long MillisecondsToMatchRouteRule { get; set; }

	public long MillisecondsToWaitingForRPSMax { get; set; }

	public long MillisecondsToCreateRequestHashCode { get; set; }

	public bool IsResponseGetFromEndPoint { get; set; }

	public long MillisecondsToFindResponseCache { get; set; }

	public long MillisecondsToRoute
	{
		get => (this.MillisecondsToMatchRouteRule + this.MillisecondsToWaitingForRPSMax
			+ this.MillisecondsToCreateRequestHashCode + this.MillisecondsToFindResponseCache);
	}

	public long MillisecondsToGetOriginalResponseCache { get; set; }

	public long MillisecondsToCreateResponseCache { get; set; }

	public long MillisecondsToSendResponse { get; set; }

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public RouteStatisticsInfoRecorder(RecorderType type)
	{
		_type = type;

		_routeTime = DateTime.Now;

		_performanceWatch.Start();
	}

	public RouteStatisticsInfoRecorder(RouteStatisticsInfoRecorder anotherRecorder)
	{
		if (anotherRecorder == null)
		{
			return;
		}

		_type = anotherRecorder.Type;

		_routerName = anotherRecorder._routerName;

		_routeTime = anotherRecorder.RouteTime;

		this._endPointName = anotherRecorder._endPointName;

		this._endPointRequestUrl = anotherRecorder._endPointRequestUrl;

		this._endPointRequestTime = anotherRecorder._endPointRequestTime;

		this._responseTime = anotherRecorder._responseTime;

		_responseHttpStatusCode = anotherRecorder._responseHttpStatusCode;


		_isRouteFailed = anotherRecorder.IsRouteFailed;


		this.MillisecondsToMatchRouteRule = anotherRecorder.MillisecondsToMatchRouteRule;

		this.MillisecondsToWaitingForRPSMax = anotherRecorder.MillisecondsToWaitingForRPSMax;

		this.MillisecondsToCreateRequestHashCode = anotherRecorder.MillisecondsToCreateRequestHashCode;

		this.IsResponseGetFromEndPoint = anotherRecorder.IsResponseGetFromEndPoint;

		this.MillisecondsToFindResponseCache = anotherRecorder.MillisecondsToFindResponseCache;

		this.MillisecondsToGetOriginalResponseCache = anotherRecorder.MillisecondsToGetOriginalResponseCache;

		this.MillisecondsToCreateResponseCache = anotherRecorder.MillisecondsToCreateResponseCache;

		this.MillisecondsToSendResponse = anotherRecorder.MillisecondsToSendResponse;
	}

	public void RestartWatchForCurrentStatisticsSection()
	{
		_responseHttpStatusCode = 0;
		this.IsResponseGetFromEndPoint = false;
		_performanceWatch.Restart();
	}

	public void EndRequest(
	    Router router,
	    EndPointRequestInfo? endPointRequestInfo)
	{
		_routerName = router.Name;

		_endPointName = string.Empty;
		_endPointRequestUrl = null;
		if (endPointRequestInfo != null)
		{
			var endPointName = endPointRequestInfo.EndPointName;
			_endPointName = endPointName ?? string.Empty;
			_endPointRequestUrl = endPointRequestInfo.EndPointRequestUrl;
		}
	}

	public void EndMatchRouteRule()
	{
		this.MillisecondsToMatchRouteRule = _performanceWatch.ElapsedMilliseconds;
		_performanceWatch.Restart();
	}

	public void EndWaitingForRPSMax()
	{
		this.MillisecondsToWaitingForRPSMax = _performanceWatch.ElapsedMilliseconds;
		_performanceWatch.Restart();
	}

	public void EndCreateRequestHashCode()
	{
		this.MillisecondsToCreateRequestHashCode = _performanceWatch.ElapsedMilliseconds;
		_performanceWatch.Restart();
	}

	public void EndFindResponseCache()
	{
		this.MillisecondsToFindResponseCache = _performanceWatch.ElapsedMilliseconds;
		_performanceWatch.Restart();
	}

	public void EndGetOriginalResponseCache()
	{
		// !!!
		_endPointRequestTime = DateTime.Now;
		// !!!
		this.MillisecondsToGetOriginalResponseCache = _performanceWatch.ElapsedMilliseconds;
		_performanceWatch.Restart();
	}

	public void EndCreateResponseCache(int responseHttpStatusCode = 0)
	{
		_responseHttpStatusCode = responseHttpStatusCode;
		this.MillisecondsToCreateResponseCache = _performanceWatch.ElapsedMilliseconds;
		_performanceWatch.Restart();
	}

	public void EndSendResponse()
	{
		this.MillisecondsToSendResponse = _performanceWatch.ElapsedMilliseconds;
		_performanceWatch.Restart();
	}

	public void EndResponse(Router router, EndPointRequestInfo? requestInfoRouted, bool isRouteFailed, int? responseHttpStatusCode = null)
	{
		// 状态码校验失败时，保留终点状态，避免被兜底缓存的状态覆盖。
		if (responseHttpStatusCode.HasValue)
		{
			_responseHttpStatusCode = responseHttpStatusCode.Value;
		}
		this.EndRequest(router, requestInfoRouted);
		_responseTime = DateTime.Now;
		_isRouteFailed = isRouteFailed;
	}

	#endregion


	////////////////////////////////////////////////
	// @实现“IDisposable”
	////////////////////////////////////////////////

	#region 实现“IDisposable”

	public void Dispose()
	{
		_performanceWatch?.Stop();

		//
		GC.SuppressFinalize(this);
	}

	#endregion

}
