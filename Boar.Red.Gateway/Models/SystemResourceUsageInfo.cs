using System;

namespace Boar.Red.Gateway.Models;

/// <summary>一次后台采样的不可变快照；利用率范围为 0 到 1。</summary>
public sealed class SystemResourceUsageInfo(double cpuUsageRate, bool isCpuUsageRateAvailable, double memoryUsageRate, bool isMemoryUsageRateAvailable, DateTimeOffset? sampleTime, double? systemRunningDurationSeconds = null)
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public double CpuUsageRate { get; } = cpuUsageRate;
	public bool IsCpuUsageRateAvailable { get; } = isCpuUsageRateAvailable;
	public double MemoryUsageRate { get; } = memoryUsageRate;
	public bool IsMemoryUsageRateAvailable { get; } = isMemoryUsageRateAvailable;
	public DateTimeOffset? SampleTime { get; } = sampleTime;
	public double? SystemRunningDurationSeconds { get; } = systemRunningDurationSeconds;

	#endregion
}
