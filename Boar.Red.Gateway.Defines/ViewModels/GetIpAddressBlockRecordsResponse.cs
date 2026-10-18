using BaoXia.Constants.Models;

namespace Boar.Red.Gateway.Defines.ViewModels;

public class GetIpAddressBlockRecordsResponse : Response
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public IEnumerable<IpAddressBlockRecord>? BlockRecords { get; set; }

	#endregion
}