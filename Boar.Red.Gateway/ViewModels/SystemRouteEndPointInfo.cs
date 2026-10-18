namespace Boar.Red.Gateway.ViewModels;

public class SystemRouteEndPointInfo
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public long Identity { get; set; }

	/// <summary>终点原有名称；新终点未指定时使用地址作为名称。</summary>
	public string? Name { get; set; }
	public string? Address { get; set; }
	public bool IsEnable { get; set; } = true;
	public double TrafficPercent { get; set; } = 100;

	#endregion
}
