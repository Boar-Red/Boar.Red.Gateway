using Boar.Red.Gateway.Defines.ViewModels;

namespace Boar.Red.Gateway.ViewModels;

public class SaveIpBlacklistRequest
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public IpAddressBlockRecord[]? BlockRecords { get; set; }

	#endregion
}
