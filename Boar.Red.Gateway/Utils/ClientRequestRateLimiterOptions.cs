namespace Boar.Red.Gateway.Utils;

/// <summary>
/// 客户端请求限流配置。
/// </summary>
public class ClientRequestRateLimiterOptions
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	/// <summary>
	/// 是否开启按请求来源 IP 的请求限制。
	/// </summary>
	public bool IsRequestSourceIPLimitEnable { get; set; }

	/// <summary>
	/// 请求来源 IP 请求取样的时间段，单位为秒。
	/// </summary>
	public double RequestSourceIPLimitSamplingSeconds { get; set; } = 1.0;

	/// <summary>
	/// 单个请求来源 IP 在取样时间段内允许的最大请求数。
	/// </summary>
	public int RequestSourceIPLimitRequestsCountMax { get; set; } = 100;

	/// <summary>
	/// 是否开启按请求来源 IP 和目标 API 路径的请求限制。
	/// </summary>
	public bool IsRequestSourceIPAndTargetAPIPathLimitEnable { get; set; }

	/// <summary>
	/// 单个请求来源 IP 对同一目标 API 路径请求取样的时间段，单位为秒。
	/// </summary>
	public double RequestSourceIPAndTargetAPIPathLimitSamplingSeconds { get; set; } = 1.0;

	/// <summary>
	/// 单个请求来源 IP 对同一目标 API 路径在取样时间段内允许的最大请求数。
	/// </summary>
	public int RequestSourceIPAndTargetAPIPathLimitRequestsCountMax { get; set; } = 100;

	#endregion
}
