using Boar.Red.Gateway.Models;
using System.Collections.Generic;

namespace Boar.Red.Gateway.ViewModels
{
	public class RouterStateStatisticsInfoInSecond
	{
		public RouteStatisticsInfo? RouterRouteStatisticsInfo { get; set; }

		public Dictionary<string, EndPointStateInfo>? EndPointStateInfes { get; set; }

		public Dictionary<string, RouteStatisticsInfo>? EndPointRouteStatisticsInfes { get; set; }
	}
}
