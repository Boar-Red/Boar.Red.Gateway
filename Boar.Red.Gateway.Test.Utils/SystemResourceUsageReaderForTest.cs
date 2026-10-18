using Boar.Red.Gateway.Utils;
using System;
using System.Threading;

namespace Boar.Red.Gateway.Test.Utils;

public sealed class SystemResourceUsageReaderForTest : ISystemResourceUsageReader
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	private int _cpuReadCount;
	private int _memoryReadCount;
	private int _runningDurationReadCount;

	public double CpuUsageRate { get; set; } = 0.25;
	public double MemoryUsageRate { get; set; } = 0.6;
	public bool IsCpuAvailable { get; set; } = true;
	public bool IsMemoryAvailable { get; set; } = true;
	public double RunningDurationSeconds { get; set; } = 86400.5;
	public bool IsRunningDurationAvailable { get; set; } = true;
	public Exception? RunningDurationException { get; set; }
	public Action? WillReadCpu { get; set; }
	public Exception? CpuException { get; set; }
	public Exception? MemoryException { get; set; }
	public int CpuReadCount => Volatile.Read(ref _cpuReadCount);
	public int MemoryReadCount => Volatile.Read(ref _memoryReadCount);
	public int RunningDurationReadCount => Volatile.Read(ref _runningDurationReadCount);
	public bool IsDisposed { get; private set; }

	#endregion

	////////////////////////////////////////////////
	// @接口实现
	////////////////////////////////////////////////

	#region 接口实现

	public bool TryReadCpuUsageRate(out double usageRate)
	{
		Interlocked.Increment(ref _cpuReadCount);
		WillReadCpu?.Invoke();
		if (CpuException != null)
		{
			throw CpuException;
		}
		usageRate = CpuUsageRate;
		return IsCpuAvailable;
	}

	public bool TryReadMemoryUsageRate(out double usageRate)
	{
		Interlocked.Increment(ref _memoryReadCount);
		if (MemoryException != null)
		{
			throw MemoryException;
		}
		usageRate = MemoryUsageRate;
		return IsMemoryAvailable;
	}

	public bool TryReadSystemRunningDurationSeconds(out double durationSeconds)
	{
		Interlocked.Increment(ref _runningDurationReadCount);
		if (RunningDurationException != null)
		{
			throw RunningDurationException;
		}
		durationSeconds = RunningDurationSeconds;
		return IsRunningDurationAvailable;
	}

	public void Dispose()
	{
		IsDisposed = true;
	}

	#endregion
}
