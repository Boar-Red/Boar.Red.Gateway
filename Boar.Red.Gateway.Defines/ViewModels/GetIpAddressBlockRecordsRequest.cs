using BaoXia.Utils.Models;
using Boar.Red.Service.BrService.ViewModels;

namespace Boar.Red.Gateway.Defines.ViewModels;

public class GetIpAddressBlockRecordsRequest(int clientUserId, ClientIpInfo clientIpInfo)
	: ServiceAdministratorRequest(clientUserId, clientIpInfo)
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public IEnumerable<string>? TargetIpAddreses { get; set; }

	#endregion
}