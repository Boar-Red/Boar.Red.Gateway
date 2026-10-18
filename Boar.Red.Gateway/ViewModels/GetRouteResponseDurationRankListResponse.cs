using BaoXia.Constants.Models;
using System.Collections.Generic;

namespace Boar.Red.Gateway.ViewModels;

public class GetRouteResponseDurationRankListResponse : Response
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public int AllRouteStatisticsInfoRecordersCount { get; set; }

	public int PageIndex { get; set; }

	public int PageSize { get; set; }

	public IEnumerable<RouteStatisticsInfoRecorder>? RouteStatisticsInfoRecorders { get; set; }

	#endregion
}