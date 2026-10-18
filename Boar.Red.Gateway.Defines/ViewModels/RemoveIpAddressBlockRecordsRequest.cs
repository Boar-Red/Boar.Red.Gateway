using BaoXia.Constants;
using BaoXia.Constants.Models;
using BaoXia.Utils.Extensions;
using BaoXia.Utils.Models;
using Boar.Red.Service.BrService.ViewModels;

namespace Boar.Red.Gateway.Defines.ViewModels;

public class RemoveIpAddressBlockRecordsRequest(int clientUserId, ClientIpInfo clientIpInfo)
	: ServiceAdministratorRequest(clientUserId, clientIpInfo)
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public IEnumerable<IpAddressBlockRecord> BlockRecords { get; set; } = default!;

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

		if (BlockRecords.IsEmpty())
		{
			return new Response()
			{
				Error = Error.InvalidRequest,
				ErrorDescription = $"请求无效，“{nameof(BlockRecords)}”为“空”。"
			};
		}

		return null;
	}

	#endregion
}