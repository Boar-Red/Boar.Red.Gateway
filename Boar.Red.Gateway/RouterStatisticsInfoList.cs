using Boar.Red.Gateway.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Boar.Red.Gateway;

public class RouterStatisticsInfoList : List<RouteStatisticsInfo>
{
	public ConcurrentDictionary<string, List<RouteStatisticsInfo>>? EndPointRouteStatisticsInfes { get; set; }
}
