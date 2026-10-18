namespace Boar.Red.Gateway.ViewModels;

public class SystemRouteRuleInfo
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public long Identity { get; set; }
	public string? Name { get; set; }
	public string? AllocationMode { get; set; }
	public string? MatchPath { get; set; }
	public double TimeoutSeconds { get; set; }
	public string? CacheMode { get; set; }
	public double CacheRefreshSeconds { get; set; }
	public string? StagedTrafficControl { get; set; }
	public SystemRouteEndPointInfo[]? EndPoints { get; set; }

	#endregion
}
