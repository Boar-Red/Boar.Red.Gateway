using BaoXia.Constants.Models;
using BaoXia.Utils.Extensions;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Defines.ViewModels;
using Boar.Red.Service.Attributes;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace Boar.Red.Gateway.Controllers;

public class IpAddressBlockRecordController : Controller
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[HttpPost]
	[ServiceAdministratorApi]
	public IActionResult AddBlockRecords([FromBody] AddIpAddressBlockRecordsRequest request)
	{
		Config.BlockRecord.AddBlockRecords(request.BlockRecords);

		return Json(new Response());
	}

	[HttpPost]
	[ServiceAdministratorApi]
	public IActionResult UpdateBlockRecord([FromBody] UpdateIpAddressBlockRecordRequest request)
	{
		var response = new Response();
		if (!Config.BlockRecord.TryUpdateBlockRecord(request.OriginalTargetIpAddress,
			request.OriginalBlockSourceName, request.BlockRecord, out var errorDescription))
		{
			response.Error = BaoXia.Constants.Error.InvalidRequest;
			response.ErrorDescription = errorDescription;
		}
		return Json(response);
	}

	[HttpPost]
	[ServiceAdministratorApi]
	public IActionResult RemoveBlockRecords([FromBody] RemoveIpAddressBlockRecordsRequest request)
	{
		Config.BlockRecord.RemoveBlockRecords(request.BlockRecords);

		return Json(new Response());
	}

	[HttpPost]
	[ServiceAdministratorApi]
	public IActionResult GetBlockRecords([FromBody] GetIpAddressBlockRecordsRequest request)
	{
		List<IpAddressBlockRecord>? targetBlockRecords = null;
		var allBlockRecords = Config.BlockRecord.BlockRecords;
		if (allBlockRecords?.Length > 0)
		{
			targetBlockRecords = [];
			if (request.TargetIpAddreses is { } targetIpAddresses && targetIpAddresses.IsNotEmpty())
			{
				foreach (var blockRecord in allBlockRecords)
				{
					foreach (var targetIpAddress in targetIpAddresses)
					{
						if (blockRecord.IsIpAddressMatched(targetIpAddress))
						{
							targetBlockRecords.Add(blockRecord);
						}
					}
				}
			}
			else
			{
				targetBlockRecords.AddRange(allBlockRecords);
			}
		}
		return Json(new GetIpAddressBlockRecordsResponse()
		{
			BlockRecords = targetBlockRecords
		});
	}

	#endregion
}
