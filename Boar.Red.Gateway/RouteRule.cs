using BaoXia.Utils.Extensions;
using BaoXia.Utils.Models;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Extensions;
using Boar.Red.Gateway.Models;
using Boar.Red.Gateway.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Boar.Red.Gateway;

public class RouteRule
{
	////////////////////////////////////////////////
	// @静态常量
	////////////////////////////////////////////////

	#region 静态常量

	public static readonly string RequestPathSpliter = "/";
	public static readonly char[] RequestPathSpliter_CharArray = ['/'];
	public const int EndPointResponseForwardBytesCountDefault = 1024 * 64;
	public const int EndPointResponseForwardBytesCountForServerSentEventsDefault = 1024 * 4;

	#endregion



	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	/// <summary>
	/// 路由排序索引值，较低的索引值优先匹配。
	/// </summary>
	public int OrderIndex { get; set; }

	protected string? _name;

	/// <summary>
	/// 路由规则名称，由配置的多个路由规则拼接而成。
	/// </summary>
	public string? Name
	{
		get
		{
			var name = _name;
			if (name == null
			    || name.Length < 1)
			{
				if (_clientRequestPaths?.Length > 0)
				{
					foreach (var requestPath in _clientRequestPaths)
					{
						if (name == null)
						{
							name = requestPath;
						}
						else
						{
							name += "\r\n" + requestPath;
						}
					}
				}
			}
			return name;
		}
		set
		{
			_name = value;
		}
	}

	/// <summary>
	/// 生效的，起始时间。
	/// </summary>
	public DateTime? ValidBeginTime { get; set; }

	/// <summary>
	/// 生效的，结束时间。
	/// </summary>
	public DateTime? ValidEndTime { get; set; }

	/// <summary>
	/// 当前路由规则是否合法有效。
	/// </summary>
	//public bool IsValid
	//{
	//        get
	//        {
	//                if (this.ClientRequestPaths?.Length > 0
	//                    && this.EndPoints?.Length > 0)
	//                {
	//                        return true;
	//                }
	//                return false;
	//        }
	//}


	////////////////////////////////////////////////
	// @自身属性，客户端请求，相关信息
	////////////////////////////////////////////////


	protected string[]? _clientRequestPaths;
	protected List<UrlPathMatchRule>? _clientRequestPathRules;

	/// <summary>
	/// 请求路径数组，即匹配规则字符串数组。
	/// </summary>
	public string[]? ClientRequestPaths
	{
		get
		{
			return _clientRequestPaths;
		}
		set
		{
			if (_clientRequestPaths != value)
			{
				_clientRequestPaths = value;
				if (_clientRequestPaths?.Length > 0)
				{
					_clientRequestPathRules = [];
					foreach (var requestPath in _clientRequestPaths)
					{
						var requestPathRule = UrlPathMatchRule.PathRuleFrom(requestPath);
						if (requestPathRule != null)
						{
							_clientRequestPathRules.Add(requestPathRule);
						}
					}
				}
			}
		}
	}

	protected string? _clientRequestPathFormat;
	protected string[]? _clientRequestPathFormatSections;

	/// <summary>
	/// 名称格式，用法如下：
	/// "*/0/*/1/*"
	/// “*”，表示该【路径块】对应的内容，使用请求Url中对应的【路径块】，
	/// 非“*”，表示【路径块】对应的内容，使用指定格式Url中对应的【路径块】。
	/// </summary>
	public string? ClientRequestPathFormat
	{
		get
		{
			return _clientRequestPathFormat;
		}
		set
		{
			if (_clientRequestPathFormat == value)
			{
				return;
			}

			_clientRequestPathFormat = value;
			_clientRequestPathFormatSections = null;
			if (_clientRequestPathFormat?.Length > 0)
			{
				_clientRequestPathFormatSections = _clientRequestPathFormat.Split(RouteRule.RequestPathSpliter_CharArray);
			}
		}
	}


	////////////////////////////////////////////////
	// @自身属性，终点请求，相关信息
	////////////////////////////////////////////////

	/// <summary>
	/// 路由终点，即真实请求的Url格式字符串，如：
	/// http://192.168.0.1/#Path#，
	/// ”#Path#“，会被替换为原始请求中的路由名称。
	/// </summary>
	public EndPoint[]? EndPoints { get; set; }

	/// <summary>
	/// 终点路由策略，即如何使用填写的路由终点，默认值为：Manual，手动策略。
	/// </summary>
	[JsonIgnore]
	public EndPointRoutePolicy EndPointRoutePolicy { get; set; } = EndPointRoutePolicy.Manual;

	/// <summary>
	/// 终点路由策略名称，即如何使用填写的路由终点，默认值为：Manual，手动策略。
	/// </summary>
	public String? EndPointRoutePolicyName
	{
		get
		{
			return BaoXia.Utils
			    .EnumUtil
			    .NameOf(
			    this.EndPointRoutePolicy);

		}
		set
		{
			if (value != null)
			{
				this.EndPointRoutePolicy
				    = BaoXia.Utils
				    .EnumUtil.
				    ValueOf(
				    value,
				    this.EndPointRoutePolicy);
			}
			else
			{
				this.EndPointRoutePolicy = EndPointRoutePolicy.Manual;
			}
		}
	}

	/// <summary>
	/// 是否唯一的终点强制可用。
	/// </summary>
	public bool IsSoleEndPointForcedEnable { get; set; }

	/// <summary>
	/// 终点请求的超时秒数。
	/// ServerSentEvents 模式下仅约束等待响应头，不约束已建立响应流的持续时间。
	/// </summary>
	public double EndPointRequestTimeoutSeconds { get; set; }

	/// <summary>
	/// 终点响应转发模式，默认等待终点响应完整后再发送给客户端。
	/// </summary>
	[JsonIgnore]
	public EndPointResponseForwardMode EndPointResponseForwardMode { get; set; }

	/// <summary>
	/// 终点响应转发模式名称，可选值有：
	/// WaitForResponseCompleted，等待终点响应完整后再发送给客户端；
	/// ForwardByBytes，按指定字节数接收并立即转发给客户端；
	/// ServerSentEvents，使用 Server-Sent Events 协议实时转发给客户端。
	/// </summary>
	public string? EndPointResponseForwardModeName
	{
		get
		{
			return BaoXia.Utils
				.EnumUtil
				.NameOf(
				this.EndPointResponseForwardMode);
		}
		set
		{
			this.EndPointResponseForwardMode
				= BaoXia.Utils
				.EnumUtil
				.ValueOf(
				value,
				this.EndPointResponseForwardMode);
		}
	}

	protected int _endPointResponseForwardBytesCount;

	/// <summary>
	/// 流式转发时每次读取并发送的字节数。
	/// ServerSentEvents 模式的默认值为：4 KB，其他模式的默认值为：64 KB。
	/// 设置为小于等于“0”时，使用默认值。
	/// </summary>
	public int EndPointResponseForwardBytesCount
	{
		get
		{
			if (_endPointResponseForwardBytesCount > 0)
			{
				return _endPointResponseForwardBytesCount;
			}
			if (this.EndPointResponseForwardMode
				== EndPointResponseForwardMode.ServerSentEvents)
			{
				return EndPointResponseForwardBytesCountForServerSentEventsDefault;
			}
			return EndPointResponseForwardBytesCountDefault;
		}
		set
		{
			_endPointResponseForwardBytesCount = value;
		}
	}

	/// <summary>
	/// 响应异常的处理类型。
	/// </summary>
	public ResponseExceptionProcess EndPointResponseExceptionProcess = ResponseExceptionProcess.ReturnExceptionInfo;

	/// <summary>
	/// 响应异常的处理类型，可选值有：
	/// ReturnExceptionInfo，返回响应异常信息；
	/// ReturnLastResponseCache，返回上一个正常的响应缓存。
	/// </summary>
	public string? EndPointResponseExceptionProcessName
	{
		get
		{
			return BaoXia.Utils
			    .EnumUtil
			    .NameOf(
			    this.EndPointResponseExceptionProcess);

		}
		set
		{
			this.EndPointResponseExceptionProcess
			    = BaoXia.Utils
			    .EnumUtil.
			    ValueOf(
			    value,
			    this.EndPointResponseExceptionProcess);
		}
	}

	#endregion


	////////////////////////////////////////////////
	// @自身属性，客户端响应缓存，相关信息
	////////////////////////////////////////////////

	#region

	/// <summary>
	/// 当前缓存模式，默认不缓存。
	/// </summary>
	[JsonIgnore]
	public ResponseCacheMode ClientResponseCacheMode { get; set; } = ResponseCacheMode.NoneCache;

	/// <summary>
	/// 响应缓存模式，可选值有：
	/// NoneCache，不缓存；
	/// ReturnCache_AutoUpdateCache，返回缓存，并自动定时更新缓存；
	/// ReturnCache_UpdateCacheByClientRequest，返回缓存，并由在每次请求时更新缓存；
	/// ReturnCache_UpdateCacheByObjectServer，返回缓存，并由目标服务器更新缓存。
	/// </summary>
	public string? ClientResponseCacheModeName
	{
		get
		{
			return BaoXia.Utils
			    .EnumUtil
			    .NameOf(
			    this.ClientResponseCacheMode);

		}
		set
		{
			this.ClientResponseCacheMode
			    = BaoXia.Utils
			    .EnumUtil.
			    ValueOf(
			    value,
			    this.ClientResponseCacheMode);
		}
	}

	/// <summary>
	/// 缓存的更新时间间隔，单位为：秒，设置为“0”时，缓存永不过期，默认为：60秒。
	/// </summary>
	public double ClientResponseCacheUpdateIntervalSeconds { get; set; } = 60.0;

	/// <summary>
	/// 计算请求哈希值的，HTTP头部关键字数组。
	/// </summary>
	public string[]? ClientRequestHttpHeaderHashKeys { get; set; }

	/// <summary>
	/// 转发请求的，HTTP头部关键字数组，
	/// 未指定时，走RouterConfig的默认设置。
	/// </summary>
	public string[]? ClientRequestHttpHeaderValidKeys { get; set; }

	protected ClientRequestRateLimiterOptions? _clientRequestRateLimiterOptions;
	protected ClientRequestRateLimiter? _clientRequestRateLimiter;

	/// <summary>
	/// 当前路由规则的客户端请求限流配置；未配置时不限制请求频率。
	/// </summary>
	public ClientRequestRateLimiterOptions? ClientRequestRateLimiterOptions
	{
		get
		{
			return _clientRequestRateLimiterOptions;
		}
		set
		{
			_clientRequestRateLimiterOptions = value;
			_clientRequestRateLimiter = value != null
				? new ClientRequestRateLimiter(value)
				: null;
		}
	}

	/// <summary>
	/// 当前路由规则的客户端请求限流器。
	/// </summary>
	[JsonIgnore]
	public ClientRequestRateLimiter? ClientRequestRateLimiter =>
		_clientRequestRateLimiter;

	/// <summary>
	/// 当去响应是否需要缓存。
	/// </summary>
	public bool IsClientResponseNeedCache
	{
		get
		{
			if (this.ClientResponseCacheMode != ResponseCacheMode.NoneCache)
			{
				return true;
			}
			return false;
		}
	}

	#endregion




	////////////////////////////////////////////////
	// @类方法
	////////////////////////////////////////////////

	#region 类方法

	/// <summary>
	/// 获取指定原始请求路径的路由路径信息。
	/// </summary>
	/// <param name="clientRequestPath">原始的请求路径。</param>
	/// <param name="clientQueryString">原始的请求查询字符串。</param>
	/// <returns>指定请求对象的路由名称。</returns>
	public static ClientRequestPathInfo? CreateClientRequestPathInfoWithClientRequestPath(
	    string? clientRequestPath,
	    string? clientQueryString)
	{
		return CreateClientRequestPathInfoWithClientRequestPath(clientRequestPath, clientQueryString, RunTime.RouteRequestPaths);
	}

	public static ClientRequestPathInfo? CreateClientRequestPathInfoWithClientRequestPath(string? clientRequestPath, string? clientQueryString, RouteRequestPathOptions options)
	{
		if (clientRequestPath == null
		    || clientRequestPath.Length < 1)
		{
			return null;
		}

		var requestPathSpliterPrefixIndex = clientRequestPath.IndexOf(options.PathSpliterPrefix, StringComparison.OrdinalIgnoreCase);
		if (requestPathSpliterPrefixIndex >= 0)
		{
			var requestCatalogPathBeginIndex
			    = (requestPathSpliterPrefixIndex
			    + options.PathSpliterPrefix.Length);
			var requestCatalogPathEndIndex
			    = clientRequestPath.IndexOf(
			    PathKeywords.RouteRequestPathSpliterSuffix,
			    requestCatalogPathBeginIndex);
			if (requestCatalogPathEndIndex < 0)
			{
				requestCatalogPathEndIndex = clientRequestPath.Length;
			}
			if (requestCatalogPathEndIndex >= requestCatalogPathBeginIndex)
			{
				string? routeRequestCatalogPath = null;
				if ((requestCatalogPathEndIndex - requestCatalogPathBeginIndex)
				    > 0)
				{
					routeRequestCatalogPath
					    = clientRequestPath[requestCatalogPathBeginIndex..requestCatalogPathEndIndex];
				}

				if (options.PathSpliter.Length == 0 && (requestPathSpliterPrefixIndex != 0 || string.IsNullOrEmpty(routeRequestCatalogPath) || options.IsReservedCatalogPath(routeRequestCatalogPath)))
				{
					return null;
				}

				// !!!
				var requestRelativePath
				    = requestCatalogPathEndIndex < clientRequestPath.Length
				    ? clientRequestPath[
				    (requestCatalogPathEndIndex
				    + PathKeywords.RouteRequestPathSpliterSuffix.Length)..]
				    : null;
				// !!!

				return new ClientRequestPathInfo(
				    routeRequestCatalogPath,
				    requestRelativePath,
				    clientQueryString);
			}
		}
		return null;
	}

	/// <summary>
	/// 获取指定请求对象的路由路径信息。
	/// </summary>
	/// <param name="request">指定的请求对象。</param>
	/// <returns>指定请求对象的路由名称。</returns>
	public static ClientRequestPathInfo? CreateClientRequestPathInfoWithClientRequest(HttpRequest request)
	{
		return RouteRule.CreateClientRequestPathInfoWithClientRequestPath(
		    request.Path.Value,
		    request.QueryString.Value);
	}


	/// <summary>
	/// 从指定的终点中，根据路由流量比例获取一个可用终点。
	/// </summary>
	/// <param name="endPoints">指定的终点数组。</param>
	/// <param name="routeTime">指定的路由时间帧。</param>
	/// <param name="endPointRoutePolicy">指定的路由终点选择策略。</param>
	/// <param name="namesOfEndPointExcepted">要排除的终点名称。</param>
	/// <returns>根据路由流量比例返回一个可用终点。</returns>
	protected static EndPoint? PickEndPointByRouteRateAtRouteTimeFromeEndPoinst(
	    ICollection<EndPoint> endPoints,
	    DateTime routeTime,
	    EndPointRoutePolicy endPointRoutePolicy,
	    ICollection<string>? namesOfEndPointExcepted)
	{
		EndPoint? endPointPicked = null;
		int allEndPointsCount = 0;
		double allRouteRate = 0.0;
		int allRouteRateDecimalDigits = 0;
		foreach (var endPoint in endPoints)
		{
			if (endPoint != null
			    && endPoint.IsConnectionEnableForEndPointRoutePolicy(endPointRoutePolicy) == true)
			{
				if (namesOfEndPointExcepted?.Contains(endPoint.Name) != true)
				{
					var endPointRouteRateControlInfo
					    = endPoint.GetRouteRateConrollInfoAtRouteTime(routeTime);

					// !!!
					endPointPicked ??= endPoint;
					// !!!

					allEndPointsCount++;
					allRouteRate += endPointRouteRateControlInfo.RouteRate;
					if (allRouteRateDecimalDigits < endPointRouteRateControlInfo.RouteRateDecimalDigits)
					{
						allRouteRateDecimalDigits = endPointRouteRateControlInfo.RouteRateDecimalDigits;
					}
				}
			}
		}

		// !!!
		var allRouteRatePowValue = Math.Pow(10, allRouteRateDecimalDigits);
		var allRouteRateInInt = (int)(allRouteRate * allRouteRatePowValue);
		// !!!
		if (allRouteRateInInt <= 0)
		{
			return endPointPicked;
		}



		// !!!
		var nowTicks = DateTime.Now.Ticks;
		var objectEndPointRouteRateIndex
		    = nowTicks
		    % allRouteRateInInt;

		// !!!
		var endPointRouteRateRangeBegin = 0.0;
		foreach (var endPoint in endPoints)
		{
			if (endPoint != null
			    && endPoint.IsConnectionEnableForEndPointRoutePolicy(endPointRoutePolicy) == true)
			{
				if (namesOfEndPointExcepted?.Contains(endPoint.Name) != true)
				{
					var endPointRouteRateControlInfo
					    = endPoint.GetRouteRateConrollInfoAtRouteTime(routeTime);
					var endPointRouteRateRangeEnd
					    = endPointRouteRateRangeBegin
					    + endPointRouteRateControlInfo.RouteRate
					    * allRouteRatePowValue;
					if (objectEndPointRouteRateIndex >= endPointRouteRateRangeBegin
					    && objectEndPointRouteRateIndex < endPointRouteRateRangeEnd)
					{
						// !!!
						endPointPicked = endPoint;
						break;
						// !!!
					}
					endPointRouteRateRangeBegin = endPointRouteRateRangeEnd;
				}
			}
		}
		return endPointPicked;
	}

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	/// <summary>
	/// 默认构造函数。
	/// </summary>
	public RouteRule()
	{ }

	/// <summary>
	/// 使用路由名称，路由地址构建路由对象。
	/// </summary>
	/// <param name="orderIndex">排序索引。</param>
	/// <param name="clientRequestPaths">客户端请求路径数组。</param>
	/// <param name="clientRequestPathFormat">客户端请求路径格式字符串。</param>
	/// <param name="endPoints">路由地址就集。</param>
	/// <param name="clientResponseCacheMode">当前路由规则的缓存模式。</param>
	/// <param name="clientResponseCacheUpdateIntervalSeconds">缓存的有效时长，单位为：秒。</param>
	public RouteRule(
	    int orderIndex,
	    //
	    string[] clientRequestPaths,
	    string clientRequestPathFormat,
	    //
	    EndPoint[] endPoints,
	    //
	    ResponseCacheMode clientResponseCacheMode,
	    double clientResponseCacheUpdateIntervalSeconds)
	{
		this.OrderIndex = orderIndex;

		this.ClientRequestPaths = clientRequestPaths;
		this.ClientRequestPathFormat = clientRequestPathFormat;

		this.EndPoints = endPoints;

		this.ClientResponseCacheMode = clientResponseCacheMode;
		this.ClientResponseCacheUpdateIntervalSeconds = clientResponseCacheUpdateIntervalSeconds;
	}

	/// <summary>
	/// 路由规则是否适用于指定的请求。
	/// </summary>
	/// <param name="requestPath">指定的请求路径。</param>
	/// <returns>当前路由规则适用于指定的请求时，返回：true，否则返回：false。</returns>
	public bool IsEnableToMatchedClientRequestPathInfo(
	    ClientRequestPathInfo? clientRequestPathInfo,
	    DateTime routeTime)
	{
		if (clientRequestPathInfo?.IsValid != true
		    || _clientRequestPathRules == null
		    || _clientRequestPathRules.Count <= 0)
		{
			return false;
		}

		if (ValidBeginTime != null
		    && ValidEndTime != null)
		{
			var routeTimeHour = routeTime.Hour;
			var routeTimeMinute = routeTime.Minute;
			var validBeginTime = ValidBeginTime.Value;
			var validEndTime = ValidEndTime.Value;
			// 结束时间的小时数大于开始时间的小时数：
			if (validEndTime.Hour >= validBeginTime.Hour)
			{
				if (routeTimeHour < validBeginTime.Hour
				    || (routeTimeHour == validBeginTime.Hour && routeTimeMinute < validBeginTime.Minute)
				    || routeTimeHour > validEndTime.Hour
				    || (routeTimeHour == validEndTime.Hour && routeTimeMinute >= validEndTime.Minute))
				{
					return false;
				}
			}
			// 结束时间的小时数小于开始时间的小时数：
			else
			{
				if ((routeTimeHour >= validEndTime.Hour
				    && routeTimeMinute >= validEndTime.Minute)
				    && (routeTimeHour <= validBeginTime.Hour
				    && routeTimeMinute < validBeginTime.Minute))
				{
					return false;
				}
			}
		}


		foreach (var requestPathRule in _clientRequestPathRules)
		{
			if (requestPathRule.IsMatched(clientRequestPathInfo))
			{
				if (this.IsSoleEndPointForcedEnable)
				{
					return true;
				}
				var endPoints = this.EndPoints;
				if (endPoints?.Length > 0)
				{
					foreach (var endPoint in endPoints)
					{
						if (endPoint?.IsConnectionEnable == true)
						{
							return true;
						}
					}
				}
				return false;
			}
		}

		return false;
	}

	/// <summary>
	/// 判断指定客户端请求是否允许通过当前路由规则。
	/// </summary>
	/// <param name="connectionIpEndPoints">请求来源 IP链路信息。</param>
	/// <param name="targetAPIPath">请求目标 API 路径。</param>
	/// <returns>允许通过时为 <see langword="true" />，否则为 <see langword="false" />。</returns>
	public bool IsClientRequestAllowed(ConnectionIpEndPoints? connectionIpEndPoints, string? targetAPIPath)
	{
		var clientRequestRateLimiter = _clientRequestRateLimiter;
		if (clientRequestRateLimiter == null)
		{
			return true;
		}
		if (connectionIpEndPoints == null || targetAPIPath == null)
		{
			return false;
		}
		var keyIpEndPointAddress = connectionIpEndPoints.KeyIpEndPointAddress;
		if (string.IsNullOrEmpty(keyIpEndPointAddress))
		{
			return false;
		}
		return clientRequestRateLimiter.IsRequestAllowed(keyIpEndPointAddress, targetAPIPath);
	}

	/// <summary>
	/// 格式化请求路由名称。
	/// </summary>
	/// <param name="requestRelativePath">原始请求的相对路径。</param>
	/// <returns>格式化后的路由请求名称。</returns>
	public string FormatRequestRelativePath(
	    string requestRelativePath)
	{
		if (requestRelativePath.Length < 1
		    || _clientRequestPathFormatSections == null
		    || _clientRequestPathFormatSections.Length < 1)
		{
			return requestRelativePath;
		}

		string? requestPath_Query = null;
		var queryMarkIndexInRequestRelativePath
		    = requestRelativePath.IndexOf(PathKeywords.QuerySymbol);
		if (queryMarkIndexInRequestRelativePath >= 0)
		{
			requestPath_Query
			    = requestRelativePath[
			    queryMarkIndexInRequestRelativePath..];
			requestRelativePath
			    = requestRelativePath[..queryMarkIndexInRequestRelativePath];
		}
		if (requestRelativePath.Length > 0)
		{
			var requestPathSections
			    = requestRelativePath.Split(RouteRule.RequestPathSpliter_CharArray);

			// !!!⚠ 通过路由名称格式化， ⚠!!!
			// !!!⚠ 将不同请求，但同一响应的请求汇总聚合 ⚠!!!
			if (requestPathSections?.Length > 0)
			{
				string[]? requestPathSectionsFormatted = null;

				var sectionsCount
				    = requestPathSections.Length < _clientRequestPathFormatSections.Length
				    ? requestPathSections.Length
				    : _clientRequestPathFormatSections.Length;
				for (var sectionIndex = 0;
				    sectionIndex < sectionsCount;
				    sectionIndex++)
				{
					var nameFormatSection = _clientRequestPathFormatSections[sectionIndex];
					if (nameFormatSection.Length > 0
					    && !nameFormatSection.Equals(PathKeywords.FuzzyMatchingKey))
					{
						requestPathSectionsFormatted ??= requestPathSections.Clone() as string[];
						// !!!
						requestPathSectionsFormatted?[sectionIndex] = nameFormatSection;
						// !!!
					}
				}
				//!!! 生成格式化后的请求Url名称 !!!
				if (requestPathSectionsFormatted != null)
				{
					requestRelativePath
					    = BaoXia.Utils.StringUtil.StringWithStrings(
					    requestPathSectionsFormatted,
					    RouteRule.RequestPathSpliter,
					    true)
					    ?? string.Empty;
				}
			}
			if (requestRelativePath.Length > 0
			    && requestPath_Query?.Length > 0)
			{
				requestRelativePath += requestPath_Query;
			}
		}
		return requestRelativePath;
	}

	/// <summary>
	/// 使用终点名称获取终点对象。
	/// </summary>
	/// <param name="endPointName">指定的终点名称。</param>
	/// <returns>指定终点名称对应的终点对象。</returns>
	public EndPoint? GetEndPointWithName(string endPointName)
	{
		if (endPointName?.Length > 0)
		{
			var endPoints = this.EndPoints;
			if (endPoints?.Length > 0)
			{
				foreach (var endPoint in endPoints)
				{
					if (endPoint?.Name?.EqualsIgnoreCase(endPointName) == true)
					{
						return endPoint;
					}
				}
			}
		}
		return null;
	}

	/// <summary>
	/// 根据路由记录的名称和地址，创建最终路由请求的地址。
	/// </summary>
	/// <param name="clientRequestPathInfo">客户端的请求路径信息。</param>
	/// <param name="routeTime">本次路由时间。</param>
	/// <param name="endPoint">对应的请求终点信息。</param>
	/// <returns>最终路由请求的地址。</returns>
	public string? CreateEndPointRequestUrlWithClientRequestPath(
	    ClientRequestPathInfo clientRequestPathInfo,
	    DateTime routeTime,
	    out EndPoint? endPoint)
	{
		return this.DidCreateEndPointRequestUrlWithClientRequestPath(
		    clientRequestPathInfo,
		    routeTime,
		    out endPoint);
	}

	/// <summary>
	/// 根据指定的请求信息，创建客户端请求信息对象。
	/// </summary>
	/// <param name="clientRequest">客户端Http请求对象。</param>
	/// <param name="clientRequestPathInfo">客户端请求路径信息。</param>
	/// <returns>由指定的请求信息，创建的客户端请求信息对象</returns>
	public async Task<RequestInfo> CreateClientRequestInfoAsync(
	    HttpRequest clientRequest,
#pragma warning disable IDE0060 // 删除未使用的参数
	    ClientRequestPathInfo clientRequestPathInfo)
#pragma warning restore IDE0060 // 删除未使用的参数
	{
		////////////////////////////////////////////////
		// 创建指定的请求相关的Http头部参数：
		////////////////////////////////////////////////
		Dictionary<string, StringValues> requestHttpHeaders = new()
    {
                                // !!!⚠ 固定的HTTP方法字段 ⚠!!!
                                {
	Constants.HttpHeaderKeys.Method,
	clientRequest.Method
	}
    };
		var requestHttpHeaderValidKeys
		    = this.ClientRequestHttpHeaderValidKeys?.Length > 0
		    ? this.ClientRequestHttpHeaderValidKeys
		    : Config.Router.ClientRequestHttpHeaderValidKeys;
		var isLastEventIdForwarded = false;
		if (requestHttpHeaderValidKeys?.Length > 0)
		{
			foreach (var httpHeaderKey
			    in
			    requestHttpHeaderValidKeys)
			{
				if (HttpHeaderKeys.Last_Event_ID.EqualsIgnoreCase(httpHeaderKey))
				{
					isLastEventIdForwarded = true;
				}
				StringValues httpHeaderValues = default;
				// Cookie的特殊处理：
				// 当Header中存在多个Cookie字段时，.Net会将多个Cookie以“,”分隔，从而产生传递错误。
				if (HttpHeaderKeys.Cookie.EqualsIgnoreCase(httpHeaderKey)
				    && Config.Router.IsClientCookieRouteWithoutEscape == false)
				{
					var clientCookies = clientRequest.Cookies;
					if (clientCookies != null)
					{
						var clientCookieStringValue = "";
						foreach (var clientCookie in clientCookies)
						{
							if (clientCookieStringValue.Length > 0)
							{
								clientCookieStringValue += "; ";
							}
							clientCookieStringValue
							    += clientCookie.Key
							    + "="
							    + clientCookie.Value;
						}
						// !!!
						httpHeaderValues = clientCookieStringValue;
						// !!!
					}
				}
				else
				{
					httpHeaderValues = clientRequest.Headers[httpHeaderKey];

				}
				if (httpHeaderValues.Count > 0)
				{
					requestHttpHeaders.Add(httpHeaderKey, httpHeaderValues);
				}
			}
		}
		if (this.EndPointResponseForwardMode
			== EndPointResponseForwardMode.ServerSentEvents
			&& isLastEventIdForwarded != true)
		{
			var lastEventIdValues = clientRequest.Headers[HttpHeaderKeys.Last_Event_ID];
			if (lastEventIdValues.Count > 0)
			{
				requestHttpHeaders.Add(
					HttpHeaderKeys.Last_Event_ID,
					lastEventIdValues);
			}
		}

		////////////////////////////////////////////////
		// 创建请求主体字节数组：
		////////////////////////////////////////////////
		byte[]? requestBodyBytes = null;
		if (clientRequest.ContentLength > 0)
		{
			var contentLength = (int)clientRequest.ContentLength;
			requestBodyBytes = new byte[contentLength];
			var bytesCount = contentLength;
			var bytesCountRead = 0;
			while (bytesCountRead < bytesCount)
			{
				var memoryBuffer
				    = new Memory<byte>(
				    requestBodyBytes,
				    bytesCountRead,
				    (bytesCount - bytesCountRead));
				bytesCountRead += await clientRequest.Body.ReadAsync(memoryBuffer);
			}
		}

		var endPointRequestInfo = new RequestInfo(
		    // !!!
		    clientRequest.Path.Value,
		    clientRequest.QueryString.Value,
		    // !!!
		    requestHttpHeaders,
		    requestBodyBytes);
		{ }
		return endPointRequestInfo;
	}

	/// <summary>
	/// 为请求创建哈希值字符串。
	/// </summary>
	/// <param name="clientRequest">要创建哈希值字符串的，Http请求对象。</param>
	/// <param name="clientRequestPathInfo">创建哈希值字符串的，请求路径。</param>
	/// <param name="routeTime">本次路由时间。</param>
	/// <returns>返回请求信息对应的哈希值。</returns>
	public EndPointRequestInfo? CreateEndPointRequestInfo(
	    HttpRequest clientRequest,
	    ClientRequestPathInfo clientRequestPathInfo,
	    RequestInfo clientRequestInfo,
	    DateTime routeTime)
	{
		if (clientRequest == null)
		{
			return null;
		}

		////////////////////////////////////////////////
		// 1/4，创建格式化后的路由名称：
		////////////////////////////////////////////////
		if (clientRequestPathInfo == null)
		{
			return null;
		}

		////////////////////////////////////////////////
		////////////////////////////////////////////////
		////////////////////////////////////////////////
		// !!!
		var endPointRequestUrl = this.CreateEndPointRequestUrlWithClientRequestPath(
		    clientRequestPathInfo,
		    routeTime,
		    // !!!☆
		    out var endPoint
		    // !!!☆
		    );
		if (endPoint == null)
		{
			// 没有可用的终点。
			return null;
		}
		////////////////////////////////////////////////
		////////////////////////////////////////////////
		////////////////////////////////////////////////


		var endPointRequestInfo = new EndPointRequestInfo(
		    clientRequestInfo,
		    ClientRequestHttpHeaderHashKeys,
		    //
		    endPoint,
		    endPoint.Name,
		    endPointRequestUrl,
		    this.EndPointRequestTimeoutSeconds,
		    this.EndPointResponseForwardMode,
		    this.EndPointResponseForwardBytesCount,
		    this.EndPointResponseExceptionProcess,
		    //
		    this.ClientResponseCacheMode,
		    this.ClientResponseCacheUpdateIntervalSeconds);
		{ }
		return endPointRequestInfo;
	}


	////////////////////////////////////////////////
	// @事件节点
	////////////////////////////////////////////////

	/// <summary>
	/// 根据路由记录的名称和地址，创建最终路由请求的地址。
	/// </summary>
	/// <param name="clientRequestPathInfo">客户端的请求路径信息。</param>
	/// <param name="routeTime">本次路由时间。</param>
	/// <param name="endPoint">对应的请求终点信息。</param>
	/// <returns>最终路由请求的地址。</returns>
	protected string? DidCreateEndPointRequestUrlWithClientRequestPath(
	    ClientRequestPathInfo clientRequestPathInfo,
	    DateTime routeTime,
	    out EndPoint? endPoint)
	{
		// !!!
		endPoint = null;
		// !!!

		if (clientRequestPathInfo == null
		    || !clientRequestPathInfo.IsValid)
		{
			return null;
		}

		var endPoints = this.EndPoints;
		if (endPoints == null
		    || endPoints.Length < 1)
		{
			return null;
		}
		var namesOfEndPointExcepted
		    = clientRequestPathInfo.NamesOfEndPointExcepted;


		string? endPointUrl;
		switch (this.EndPointRoutePolicy)
		{
			default:
			case EndPointRoutePolicy.Manual:
				{
					endPoint
					    = this.DidGetEndPointWithPolicy_Manual(
					    endPoints,
					    routeTime);
				}
				break;
			case EndPointRoutePolicy.Random:
				{
					endPoint
					    = this.DidGetEndPointWithPolicy_Random(
					    endPoints,
					    routeTime,
					    namesOfEndPointExcepted);
				}
				break;
			case EndPointRoutePolicy.MasterSlave:
				{
					endPoint
					    = this.DidGetEndPointWithPolicy_MasterSlave(
					    endPoints,
					    routeTime,
					    namesOfEndPointExcepted);
				}
				break;
		}

		if (endPoint == null)
		{
			if (this.IsSoleEndPointForcedEnable)
			{
				foreach (var endPointExisted in endPoints)
				{
					if (endPointExisted != null)
					{
						if (namesOfEndPointExcepted == null
						    || namesOfEndPointExcepted.Contains(endPointExisted.Name) != true)
						{
							if (endPoint == null
							    // 选择RPS较小的终点：
							    || endPoint.RPS > endPointExisted.RPS)
							{
								// !!! 强制使用终点时，挑选RPS最低的RPS来使用。 !!!
								endPoint = endPointExisted;
								// !!!
							}
						}
					}
				}
			}
			//if (endPoint != null)
			//{
			//        System.Diagnostics.Debug.WriteLine("强制使用终点：" + endPoint.Name);
			//}
		}
		if (endPoint == null)
		{
			return null;
		}


		var clientRequestRelativePath = clientRequestPathInfo.RelativePath;
		var clientQueryString = clientRequestPathInfo.QueryString;
		if (clientRequestRelativePath == null)
		{
			return null;
		}

		// !!!⚠ 格式化路由名称 ⚠!!!
		var requestRouteRelativePathFormatted = this.FormatRequestRelativePath(clientRequestRelativePath);
		// !!!⚠ 格式化路由名称 ⚠!!!
		endPointUrl = endPoint.PathTemplate?.Replace(
		    PathKeywords.RouteRuleEndPointKey_RelativePath,
		    requestRouteRelativePathFormatted);
		if (clientQueryString?.Length > 0
		    && endPointUrl?.Length > 0)
		{
			if (endPointUrl.Contains(PathKeywords.QuerySymbol))
			{
				clientQueryString = clientQueryString.TrimStart(
				      PathKeywords.QuerySymbol,
				      PathKeywords.QueryParamsSpliter);
				if (endPointUrl.EndsWith(PathKeywords.QuerySymbol)
				    || endPointUrl.EndsWith(PathKeywords.QueryParamsSpliter))
				{
					endPointUrl += clientQueryString;
				}
				else
				{
					endPointUrl += PathKeywords.QueryParamsSpliter + clientQueryString;
				}
			}
			else
			{
				if (clientQueryString.StartsWith(PathKeywords.QuerySymbol))
				{
					endPointUrl += clientQueryString;
				}
				else
				{
					clientQueryString = clientQueryString.TrimStart(
					      PathKeywords.QueryParamsSpliter);
					endPointUrl += PathKeywords.QuerySymbol + clientQueryString;
				}
			}
		}
		return endPointUrl;
	}

	/// <summary>
	/// 使用【手动】策略从指定的地址组中获取路由地址。
	/// </summary>
	/// <param name="endPoints">指定的地址组。</param>
	/// <param name="routeTime">当前路由时间帧。</param>
	/// <returns>根据手动置顶获取路由终点。</returns>
	protected EndPoint? DidGetEndPointWithPolicy_Manual(
	    EndPoint[] endPoints,
	    DateTime routeTime)
	{
		if (endPoints.Length < 1)
		{
			return null;
		}

		EndPoint? endPointPicked = null;
		foreach (var endPoint in endPoints)
		{
			if (endPoint != null
			    // !!!⚠ 手动模式时，只要该终点没有被手动禁用，就可用 ⚠!!!
			    && endPoint.State != EndPointState.Disable)
			// && endPoint.IsConnectionEnable)
			{
				endPointPicked = endPoint;
				break;
			}
		}
		if (endPointPicked == null)
		{
			return null;
		}
		var endPointRouteRateControllInfo
		    = endPointPicked.GetRouteRateConrollInfoSpecifiedAtRouteTime(routeTime);
		if (endPointRouteRateControllInfo == null
		    && endPointPicked.State != EndPointState.Disabling)
		{
			return endPointPicked;
		}
		////////////////////////////////////////////////
		// !!! 当前选中的终点处于“流量控制期内”时，!!!
		// !!! 尝试根据流量比例，获取适用的终点：   !!!
		////////////////////////////////////////////////
		return RouteRule.PickEndPointByRouteRateAtRouteTimeFromeEndPoinst(
		    endPoints,
		    routeTime,
		    EndPointRoutePolicy.Manual,
		    null);
	}

	/// <summary>
	/// 使用【随机】策略从指定的地址组中获取路由地址。
	/// </summary>
	/// <param name="endPoints">指定的地址组。</param>
	/// <param name="routeTime">当前路由时间帧。</param>
	/// <param name="namesOfEndPointExcepted">要排除的终点名称。</param>
	/// <returns>根据平衡策略获取路由终点。</returns>
	protected EndPoint? DidGetEndPointWithPolicy_Random(
	    ICollection<EndPoint> endPoints,
	    DateTime routeTime,
	    ICollection<string>? namesOfEndPointExcepted)
	{
		return RouteRule.PickEndPointByRouteRateAtRouteTimeFromeEndPoinst(
		    endPoints,
		    routeTime,
		    EndPointRoutePolicy.Random,
		    namesOfEndPointExcepted);
	}

	/// <summary>
	/// 使用【主从】策略从指定的地址组中获取路由地址。
	/// </summary>
	/// <param name="endPoints">指定的地址组。</param>
	/// <param name="routeTime">当前路由时间帧。</param>
	/// <param name="namesOfEndPointExcepted">要排除的终点名称。</param>
	/// <returns>根据主从策略获取到的路由地址。</returns>
	protected EndPoint? DidGetEndPointWithPolicy_MasterSlave(
	    EndPoint[] endPoints,
	    DateTime routeTime,
	    ICollection<string>? namesOfEndPointExcepted)
	{
		if (endPoints.Length < 1)
		{
			return null;
		}

		EndPoint? endPointPicked = null;
		foreach (var endPointExisted in endPoints)
		{
			if (endPointExisted != null
			    && endPointExisted.IsConnectionEnableForEndPointRoutePolicy(EndPointRoutePolicy.MasterSlave)
			    == true)
			{
				if (namesOfEndPointExcepted == null
				    || namesOfEndPointExcepted.Contains(endPointExisted.Name) != true)
				{
					// !!!
					endPointPicked = endPointExisted;
					break;
					// !!!
				}
			}
		}
		if (endPointPicked == null)
		{
			return null;
		}
		var endPointRouteRateControllInfo
		    = endPointPicked.GetRouteRateConrollInfoSpecifiedAtRouteTime(routeTime);
		if (endPointRouteRateControllInfo == null
		    && endPointPicked.State != EndPointState.Disabling)
		{
			return endPointPicked;
		}
		////////////////////////////////////////////////
		// !!! 当前选中的终点处于“流量控制期内”时，!!!
		// !!! 尝试根据流量比例，获取适用的终点：   !!!
		////////////////////////////////////////////////
		return RouteRule.PickEndPointByRouteRateAtRouteTimeFromeEndPoinst(
		    endPoints,
		    routeTime,
		    EndPointRoutePolicy.MasterSlave,
		    namesOfEndPointExcepted);
	}
}
