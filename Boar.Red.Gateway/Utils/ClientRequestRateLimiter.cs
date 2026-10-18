using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

namespace Boar.Red.Gateway.Utils;

/// <summary>
/// 根据请求来源 IP 和目标 API 路径限制客户端请求频率。
/// </summary>
/// <remarks>
/// 此组件应以单例方式复用，才能在全部请求之间正确累计请求次数。
/// 当启用的限制参数不合法时，组件会拒绝请求，避免错误配置导致限流失效。
/// </remarks>
public class ClientRequestRateLimiter
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	readonly ClientRequestRateLimiterOptions _options;
	readonly TimeProvider _timeProvider;
	readonly ConcurrentDictionary<string, ClientRequestRateLimiterInfo> _limitInfosByRequestSourceIP = new(
		StringComparer.Ordinal);

	/// <summary>
	/// 当前限流配置。
	/// </summary>
	public ClientRequestRateLimiterOptions Options => _options;

	#endregion


	////////////////////////////////////////////////
	// @构造方法
	////////////////////////////////////////////////

	#region 构造方法

	/// <summary>
	/// 初始化客户端请求限流器。
	/// </summary>
	/// <param name="options">限流配置。</param>
	/// <param name="timeProvider">时间提供器；未指定时使用系统时间。</param>
	public ClientRequestRateLimiter(
		ClientRequestRateLimiterOptions options,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(options);

		_options = options;
		_timeProvider = timeProvider ?? TimeProvider.System;
	}

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	/// <summary>
	/// 判断当前请求是否允许正常进行；允许时会立即记录本次请求。
	/// </summary>
	/// <param name="requestSourceIP">请求来源 IP。</param>
	/// <param name="targetAPIPath">请求目标 API 路径。</param>
	/// <returns>允许请求时为 <see langword="true" />，否则为 <see langword="false" />。</returns>
	public bool IsRequestAllowed(string requestSourceIP, string targetAPIPath)
	{
		var isRequestSourceIPLimitEnable = _options.IsRequestSourceIPLimitEnable;
		var isRequestSourceIPAndTargetAPIPathLimitEnable = _options
			.IsRequestSourceIPAndTargetAPIPathLimitEnable;
		if (isRequestSourceIPLimitEnable == false
			&& isRequestSourceIPAndTargetAPIPathLimitEnable == false)
		{
			return true;
		}

		var requestSourceIPNormalized = GetRequestSourceIPNormalized(
			requestSourceIP);
		var targetAPIPathNormalized = GetTargetAPIPathNormalized(
			targetAPIPath);
		if (requestSourceIPNormalized == null
			|| targetAPIPathNormalized == null)
		{
			return false;
		}

		if (isRequestSourceIPLimitEnable
			&& IsRequestLimitConfigurationValid(
				_options.RequestSourceIPLimitSamplingSeconds,
				_options.RequestSourceIPLimitRequestsCountMax) == false)
		{
			return false;
		}
		if (isRequestSourceIPAndTargetAPIPathLimitEnable
			&& IsRequestLimitConfigurationValid(
				_options.RequestSourceIPAndTargetAPIPathLimitSamplingSeconds,
				_options.RequestSourceIPAndTargetAPIPathLimitRequestsCountMax) == false)
		{
			return false;
		}

		var limitInfo = _limitInfosByRequestSourceIP.GetOrAdd(
			requestSourceIPNormalized,
			static _ => new ClientRequestRateLimiterInfo());
		lock (limitInfo)
		{
			var requestTime = _timeProvider.GetUtcNow();
			Queue<DateTimeOffset>? requestTimesByTargetAPIPath = null;

			if (isRequestSourceIPLimitEnable)
			{
				RemoveExpiredRequestTimes(
					limitInfo.RequestTimes,
					requestTime,
					_options.RequestSourceIPLimitSamplingSeconds);
				if (limitInfo.RequestTimes.Count
					>= _options.RequestSourceIPLimitRequestsCountMax)
				{
					return false;
				}
			}

			if (isRequestSourceIPAndTargetAPIPathLimitEnable)
			{
				requestTimesByTargetAPIPath = GetRequestTimesByTargetAPIPath(
					limitInfo,
					targetAPIPathNormalized);
				RemoveExpiredRequestTimes(
					requestTimesByTargetAPIPath,
					requestTime,
					_options.RequestSourceIPAndTargetAPIPathLimitSamplingSeconds);
				if (requestTimesByTargetAPIPath.Count
					>= _options.RequestSourceIPAndTargetAPIPathLimitRequestsCountMax)
				{
					return false;
				}
			}

			if (isRequestSourceIPLimitEnable)
			{
				limitInfo.RequestTimes.Enqueue(requestTime);
			}
			if (requestTimesByTargetAPIPath != null)
			{
				requestTimesByTargetAPIPath.Enqueue(requestTime);
			}
			return true;
		}
	}

	#endregion


	////////////////////////////////////////////////
	// @私有方法
	////////////////////////////////////////////////

	#region 私有方法

	static string? GetRequestSourceIPNormalized(string? requestSourceIP)
	{
		if (string.IsNullOrWhiteSpace(requestSourceIP))
		{
			return null;
		}

		if (IPAddress.TryParse(requestSourceIP.Trim(), out var requestSourceIPAddress)
			== false)
		{
			return null;
		}
		if (requestSourceIPAddress.IsIPv4MappedToIPv6)
		{
			requestSourceIPAddress = requestSourceIPAddress.MapToIPv4();
		}
		return requestSourceIPAddress.ToString();
	}

	static string? GetTargetAPIPathNormalized(string? targetAPIPath)
	{
		if (string.IsNullOrWhiteSpace(targetAPIPath))
		{
			return null;
		}

		var targetAPIPathNormalized = targetAPIPath.Trim();
		var queryOrFragmentBeginIndex = targetAPIPathNormalized.IndexOfAny(['?', '#']);
		if (queryOrFragmentBeginIndex >= 0)
		{
			targetAPIPathNormalized = targetAPIPathNormalized[
				..queryOrFragmentBeginIndex];
		}
		if (targetAPIPathNormalized.Length < 1)
		{
			return null;
		}
		return targetAPIPathNormalized;
	}

	static bool IsRequestLimitConfigurationValid(
		double samplingSeconds,
		int requestsCountMax)
	{
		return samplingSeconds > 0.0
			&& requestsCountMax > 0;
	}

	static Queue<DateTimeOffset> GetRequestTimesByTargetAPIPath(
		ClientRequestRateLimiterInfo limitInfo,
		string targetAPIPath)
	{
		if (limitInfo.RequestTimesByTargetAPIPath.TryGetValue(
			targetAPIPath,
			out var requestTimesByTargetAPIPath))
		{
			return requestTimesByTargetAPIPath;
		}

		requestTimesByTargetAPIPath = [];
		limitInfo.RequestTimesByTargetAPIPath.Add(
			targetAPIPath,
			requestTimesByTargetAPIPath);
		return requestTimesByTargetAPIPath;
	}

	static void RemoveExpiredRequestTimes(
		Queue<DateTimeOffset> requestTimes,
		DateTimeOffset requestTime,
		double samplingSeconds)
	{
		var requestTimeMin = requestTime.AddSeconds(-samplingSeconds);
		while (requestTimes.TryPeek(out var requestTimeExisted)
			&& requestTimeExisted <= requestTimeMin)
		{
			requestTimes.Dequeue();
		}
	}

	#endregion
}
