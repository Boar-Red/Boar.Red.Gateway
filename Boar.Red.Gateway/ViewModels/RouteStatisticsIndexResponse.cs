using BaoXia.Constants.Models;
using System.Collections.Generic;

namespace Boar.Red.Gateway.ViewModels;

public class RouteStatisticsIndexResponse : Response
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public ICollection<string>? AllRouterNames { get; set; }

	public Dictionary<string, ICollection<EndPointStateInfo>>? RouterEndPointInfes { get; set; }

	#endregion
}