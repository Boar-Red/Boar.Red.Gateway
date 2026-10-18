using BaoXia.Constants;
using BaoXia.Constants.Interfaces;
using BaoXia.Constants.Models;
using BaoXia.Utils.Extensions;
using BaoXia.Utils.Interfaces;
using BaoXia.Utils.Models;
using Boar.Red.Gateway.Client.ViewModels;
using Boar.Red.Gateway.Defines.ViewModels;
using Boar.Red.Service.BrService;
using Boar.Red.Service.BrService.Extensions;
using Boar.Red.Service.BrService.ViewModels;

namespace Boar.Red.Gateway.Client;

/// <summary>
/// 服务实例对象，
/// 通常用于定义当前服务支持调用的方法（RPC客户端）。
/// </summary>
public class BrGatewayService(string? groupName, string? serviceUri, ServiceDiscoverer? serviceDiscoverer,
	string? applicationIdentity, string? applicationPassword, string? applicationAesKey, Func<BrGatewayServiceClientConfig?>? toGetClientConfig,
	Func<int, ClientIpInfo, ServiceAdministratorLoginRequest>? toGetServiceAdministratorLoginRequest,
	ILogFile? logFile, ILogFile? exceptionLogFile, ILogFile? warningLogFile, ILogFile? bxServiceLogFile, ILogFile? gatewayLogFile,
	string serviceName = Defines.Constants.ServiceInfo.ServiceName)
	: AdministratorClient(groupName, serviceName, serviceUri, serviceDiscoverer,
		applicationIdentity, applicationPassword, applicationAesKey, toGetClientConfig, toGetServiceAdministratorLoginRequest,
		logFile, exceptionLogFile, warningLogFile, bxServiceLogFile)
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	private readonly ILogFile? _gatewayLogFile = gatewayLogFile;

	#endregion


	////////////////////////////////////////////////
	// @自身实现，原始操作。
	////////////////////////////////////////////////

	#region 自身实现，原始操作。

	public async Task<Response> AddIpAddressBlockRecordsAsync(
#pragma warning disable IDE0060 // 删除未使用的参数
		IEnumerable<IpAddressBlockRecord> blockRecords, ITraceable? traceable,
#pragma warning restore IDE0060 // 删除未使用的参数
		int operatorUserId, ClientIpInfo clientIpInfo)
	{
		if (blockRecords.IsEmpty())
		{
			return new()
			{
				Error = Error.InvalidRequest,
				ErrorDescription = $"“{nameof(blockRecords)}”字段为“空”。"
			};
		}

		var request = new AddIpAddressBlockRecordsRequest(operatorUserId, clientIpInfo)
		{
			BlockRecords = blockRecords
		};
		var apiResponseInfo = await InvokeAdminApiToGetObjectAsync<Response>("ipAddressBlockRecord/addBlockRecords", request);
		{ }
		var errorResult = apiResponseInfo.GetErrorResult("添加Ip地址封禁记录失败");
		if (errorResult?.IsFailed == true)
		{
			return errorResult;
		}
		if (_gatewayLogFile is { } gatewayLogFile)
		{
			foreach (var blockRecord in blockRecords)
			{
				// !!!
				gatewayLogFile.Logs(this,
					$"已封禁Ip“{blockRecord.TargetIpAddress}”，封禁原因：{blockRecord.BlockRemark?.TrimTrailingPunctuation()}。", null);
				// !!!
			}
		}
		return apiResponseInfo.Result!;
	}

	public async Task<Response> UpdateIpAddressBlockRecordAsync(
		string originalTargetIpAddress, string originalBlockSourceName, IpAddressBlockRecord blockRecord,
		int operatorUserId, ClientIpInfo clientIpInfo)
	{
		var request = new UpdateIpAddressBlockRecordRequest(operatorUserId, clientIpInfo)
		{
			OriginalTargetIpAddress = originalTargetIpAddress,
			OriginalBlockSourceName = originalBlockSourceName,
			BlockRecord = blockRecord
		};
		var apiResponseInfo = await InvokeAdminApiToGetObjectAsync<Response>("ipAddressBlockRecord/updateBlockRecord", request);
		{ }
		var errorResult = apiResponseInfo.GetErrorResult("修改Ip地址封禁记录失败");
		if (errorResult?.IsFailed == true)
		{
			return errorResult;
		}
		_gatewayLogFile?.Logs(this,
			$"已修改Ip封禁记录“{originalTargetIpAddress}”为“{blockRecord.TargetIpAddress}”，封禁来源：{originalBlockSourceName}。", null);
		return apiResponseInfo.Result!;
	}

	public async Task<Response> AddIpAddressBlockRecordsByBlockSourceNameAsync(
		string blockSourceName, DateTimeOffset blockBeginTime, DateTimeOffset blockEndTime, string blockRemark,
		ITraceable? traceable, int operatorUserId, ClientIpInfo clientIpInfo, params string[] targetIpAddresses)
	{
		if (string.IsNullOrWhiteSpace(blockSourceName))
		{
			return new()
			{
				Error = Error.InvalidRequest,
				ErrorDescription = $"指定的封禁源名称字段（{nameof(blockSourceName)}）为“空”。"
			};
		}
		if (string.IsNullOrWhiteSpace(blockRemark))
		{
			return new()
			{
				Error = Error.InvalidRequest,
				ErrorDescription = $"指定的封禁备注字段（{nameof(blockRemark)}）为“空”。"
			};
		}


		var blockRecords = new List<IpAddressBlockRecord>();
		foreach (var targetIpAddress in targetIpAddresses)
		{
			if (string.IsNullOrWhiteSpace(targetIpAddress))
			{
				continue;
			}
			var ipAddressBlockRecord = new IpAddressBlockRecord()
			{
				BlockSourceName = blockSourceName,
				BlockBeginTime = blockBeginTime,
				BlockEndTime = blockEndTime,
				BlockRemark = blockRemark,

				TargetIpAddress = targetIpAddress
			};
			blockRecords.Add(ipAddressBlockRecord);
		}
		return await AddIpAddressBlockRecordsAsync(blockRecords, traceable, operatorUserId, clientIpInfo);
	}

#pragma warning disable IDE0060 // 删除未使用的参数
	public async Task<Response> RemoveIpAddressBlockRecordsAsync(
		IEnumerable<IpAddressBlockRecord> blockRecords, string removeRemark,
		ITraceable? traceable, int operatorUserId, ClientIpInfo clientIpInfo, DateTimeOffset operateTime)
#pragma warning restore IDE0060 // 删除未使用的参数
	{
		if (blockRecords.IsEmpty())
		{
			return new()
			{
				Error = Error.InvalidRequest,
				ErrorDescription = $"“{nameof(blockRecords)}”字段为“空”。"
			};
		}

		var request = new RemoveIpAddressBlockRecordsRequest(operatorUserId, clientIpInfo)
		{
			BlockRecords = blockRecords
		};
		var apiResponseInfo = await InvokeAdminApiToGetObjectAsync<Response>("ipAddressBlockRecord/removeBlockRecords", request);
		{ }
		var errorResult = apiResponseInfo.GetErrorResult("删除Ip地址封禁记录失败");
		if (errorResult?.IsFailed == true)
		{
			return errorResult;
		}
		if (_gatewayLogFile is { } gatewayLogFile)
		{
			foreach (var blockRecord in blockRecords)
			{
				// !!!
				gatewayLogFile.Logs(this,
					$"已解封Ip“{blockRecord.TargetIpAddress}”，解封原因：{removeRemark?.TrimTrailingPunctuation()}。", null);
				// !!!
			}
		}
		return apiResponseInfo.Result!;
	}

	public async Task<GetIpAddressBlockRecordsResponse> GetIpAddressBlockRecordsAsync(
		IEnumerable<string>? targetIpAddreses, int operatorUserId, ClientIpInfo clientIpInfo)
	{
		var request = new GetIpAddressBlockRecordsRequest(operatorUserId, clientIpInfo)
		{
			TargetIpAddreses = targetIpAddreses
		};
		var apiResponseInfo = await InvokeAdminApiToGetObjectAsync<GetIpAddressBlockRecordsResponse>(
			"ipAddressBlockRecord/getBlockRecords", request);
		{ }
		var errorResult = apiResponseInfo.GetErrorResult<GetIpAddressBlockRecordsResponse>("获取Ip地址封禁记录失败");
		if (errorResult?.IsFailed == true)
		{
			return errorResult;
		}
		return apiResponseInfo.Result!;
	}


	#endregion
}
