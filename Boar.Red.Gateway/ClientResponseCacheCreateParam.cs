using BaoXia.Utils.Models;
using Boar.Red.Gateway.Models;

namespace Boar.Red.Gateway;

public class ClientResponseCacheCreateParam(EndPointRequestInfo endRequestInfo, ConnectionIpEndPoints? connectionIpEndPoints)
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public EndPointRequestInfo EndRequestInfo { get; set; } = endRequestInfo;

	public ConnectionIpEndPoints? ConnectionIpEndPoints { get; set; } = connectionIpEndPoints;

	#endregion
}
