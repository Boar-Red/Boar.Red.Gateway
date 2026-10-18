using BaoXia.Constants;
using BaoXia.Constants.Models;
using BaoXia.Utils.Models;
using Boar.Red.Service.BrService.ViewModels;
using System.Net;

namespace Boar.Red.Gateway.Defines.ViewModels;

public class UpdateIpAddressBlockRecordRequest(int clientUserId, ClientIpInfo clientIpInfo)
	: ServiceAdministratorRequest(clientUserId, clientIpInfo)
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public string OriginalTargetIpAddress { get; set; } = default!;

	public string OriginalBlockSourceName { get; set; } = default!;

	public IpAddressBlockRecord BlockRecord { get; set; } = default!;

	#endregion

	////////////////////////////////////////////////
	// @重载
	////////////////////////////////////////////////

	#region 重载

	protected override Response? DidGetErrorResonseByCheckRequest(RequestPipelineType requestPipelineType)
	{
		var errorResponse = base.DidGetErrorResonseByCheckRequest(requestPipelineType);
		if (errorResponse?.IsFailed == true)
		{
			return errorResponse;
		}
		if (string.IsNullOrWhiteSpace(OriginalTargetIpAddress)
			|| string.IsNullOrWhiteSpace(OriginalBlockSourceName)
			|| BlockRecord == null
			|| !IPAddress.TryParse(BlockRecord.TargetIpAddress, out _)
			|| BlockRecord.TargetIpAddress.Contains('%')
			|| BlockRecord.TargetIpAddress.Contains('[')
			|| BlockRecord.TargetIpAddress.Contains(']')
			|| BlockRecord.BlockSourceName != OriginalBlockSourceName
			|| BlockRecord.BlockBeginTime == default
			|| BlockRecord.BlockEndTime <= BlockRecord.BlockBeginTime)
		{
			return new Response()
			{
				Error = Error.InvalidRequest,
				ErrorDescription = "请提供原封禁记录、有效的新IP地址与封禁时间，并保留原封禁来源。"
			};
		}
		return null;
	}

	#endregion
}
