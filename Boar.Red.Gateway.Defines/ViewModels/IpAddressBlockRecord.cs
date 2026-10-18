namespace Boar.Red.Gateway.Defines.ViewModels;

public class IpAddressBlockRecord
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public string BlockSourceName { get; set; } = default!;

	public DateTimeOffset BlockBeginTime { get; set; }

	public DateTimeOffset BlockEndTime { get; set; }

	public string? BlockRemark { get; set; }

	public string TargetIpAddress { get; set; } = default!;

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public IpAddressBlockRecord SetIpAddressBlockRecord(IpAddressBlockRecord ipAddressBlockRecord)
	{
		BlockSourceName = ipAddressBlockRecord.BlockSourceName;

		BlockBeginTime = ipAddressBlockRecord.BlockBeginTime;
		BlockEndTime = ipAddressBlockRecord.BlockEndTime;

		BlockRemark = ipAddressBlockRecord.BlockRemark;

		TargetIpAddress = ipAddressBlockRecord.TargetIpAddress;

		return this;
	}

	public bool IsIpAddressMatched(string ipAddress)
	{
		if (TargetIpAddress?.Contains(ipAddress) == true)
		{
			return true;
		}
		return false;
	}

	#endregion
}