using Boar.Red.Service.BrService.ViewModels;

namespace Boar.Red.Gateway.ViewModels;

public class GetRouteStatisticsInfesRequest : WebSocketServiceRequest
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public string? RouterName { get; set; }

	public string? EndPointName { get; set; }

	public long BeginTimeStamp { get; set; } = 0;

	public long EndTimeStamp { get; set; } = 0;

	public bool IsGetRouterRouteStateStatisticsInfoInLastSecond { get; set; } = false;

	#endregion
}