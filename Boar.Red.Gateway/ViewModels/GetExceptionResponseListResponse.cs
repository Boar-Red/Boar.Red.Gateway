using BaoXia.Constants.Models;
using System;
using System.Collections.Generic;

namespace Boar.Red.Gateway.ViewModels;

public class GetExceptionResponseListResponse : Response
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public string? RouterName { get; set; }

	public string? EndPointName { get; set; }

	public DateTime BeginTime { get; set; }

	public DateTime EndTime { get; set; }

	public ICollection<RouteStatisticsInfoRecorder> ExceptionResponseList { get; set; } = [];

	#endregion
}
