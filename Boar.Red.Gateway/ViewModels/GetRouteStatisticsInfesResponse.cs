using Boar.Red.Gateway.Models;
using Boar.Red.Service.BrService.ViewModels;
using System;
using System.Collections.Generic;

namespace Boar.Red.Gateway.ViewModels;

public class GetRouteStatisticsInfesResponse : WebSocketServiceResponse
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public string? RouterName { get; set; }

	public string? EndPointName { get; set; }

	public DateTime BeginTime { get; set; }

	public DateTime EndTime { get; set; }

	/// <summary>整机 CPU 利用率，范围为 0～1；需同时检查可用标记。</summary>
	public double SystemCpuUsageRate { get; set; }

	public bool IsSystemCpuUsageRateAvailable { get; set; }

	/// <summary>整机物理内存利用率，范围为 0～1；需同时检查可用标记。</summary>
	public double SystemMemoryUsageRate { get; set; }

	public bool IsSystemMemoryUsageRateAvailable { get; set; }

	/// <summary>共享后台快照的采样时间，每秒更新；首次采样前为空。</summary>
	public DateTimeOffset? SystemResourceUsageSampleTime { get; set; }

	/// <summary>网关程序启动以来的运行时长，单位为秒；首次采样前或采集失败时为空。</summary>
	public double? SystemRunningDurationSeconds { get; set; }

	public int SystemThreadsCountMax
	{
		get
		{
			return this.SystemWorkerThreadsCountMax
			    + this.SystemIOThreadsCountMax;
		}
	}

	public int SystemThreadsCount
	{
		get
		{
			return this.SystemWorkerThreadsCount
			    + this.SystemIOThreadsCount;
		}
	}

	public int SystemWorkerThreadsCountMax { get; set; }

	public int SystemWorkerThreadsCount { get; set; }

	public int SystemIOThreadsCountMax { get; set; }

	public int SystemIOThreadsCount { get; set; }

	public int SystemThreadTasksCountWaitingToWork { get; set; }

	public int SystemThreadsCountInPool { get; set; }


	public RouterStateStatisticsInfoInSecond? RouterStateStatisticsInfoInLastSecond { get; set; }

	public ICollection<RouteStatisticsInfo>? RouteStatisticsInfes { get; set; }


	#endregion
}