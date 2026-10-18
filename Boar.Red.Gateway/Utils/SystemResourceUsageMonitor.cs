using Boar.Red.Gateway.LogFiles;
using Boar.Red.Gateway.Models;
using System;
using System.Threading;

namespace Boar.Red.Gateway.Utils;

/// <summary>所有统计连接共用一份后台快照；读取快照不执行系统调用、文件读取或等待。</summary>
public sealed class SystemResourceUsageMonitor : IDisposable
{
	////////////////////////////////////////////////
	// @静态变量
	////////////////////////////////////////////////

	#region 静态变量

	private static readonly Lazy<SystemResourceUsageMonitor> _shared = new(() => new(new SystemResourceUsageReader()));

	public static SystemResourceUsageMonitor Shared => _shared.Value;

	#endregion

	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	private readonly ISystemResourceUsageReader _reader;
	private readonly Timer _timer;
	private SystemResourceUsageInfo _current = new(0, false, 0, false, null);
	private DateTimeOffset _lastExceptionLogTime;
	private int _isSampling;
	private int _isDisposed;

	public SystemResourceUsageInfo Current => Volatile.Read(ref _current);

	#endregion

	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	private void LogSamplingException(Exception exception)
	{
		var now = DateTimeOffset.UtcNow;
		if ((now - _lastExceptionLogTime).TotalMinutes >= 1)
		{
			_lastExceptionLogTime = now;
			Log.Exception.Logs(this, "采集系统资源利用率失败，将当前指标标记为不可用。", exception);
		}
	}

	public SystemResourceUsageMonitor(ISystemResourceUsageReader reader)
	{
		_reader = reader;
		_timer = new Timer(DidSampleTimerElapsed, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
	}

	#endregion

	////////////////////////////////////////////////
	// @事件节点
	////////////////////////////////////////////////

	#region 事件节点

	private void DidSampleTimerElapsed(object? state)
	{
		if (Volatile.Read(ref _isDisposed) != 0 || Interlocked.CompareExchange(ref _isSampling, 1, 0) != 0)
		{
			return;
		}
		try
		{
			var cpuRate = 0.0;
			var memoryRate = 0.0;
			var isCpuAvailable = false;
			var isMemoryAvailable = false;
			double? runningDurationSeconds = null;
			try
			{
				isCpuAvailable = _reader.TryReadCpuUsageRate(out cpuRate) && double.IsFinite(cpuRate);
			}
			catch (Exception exception)
			{
				LogSamplingException(exception);
			}
			try
			{
				isMemoryAvailable = _reader.TryReadMemoryUsageRate(out memoryRate) && double.IsFinite(memoryRate);
			}
			catch (Exception exception)
			{
				LogSamplingException(exception);
			}
			try
			{
				if (_reader.TryReadSystemRunningDurationSeconds(out var durationSeconds) && double.IsFinite(durationSeconds) && durationSeconds >= 0)
				{
					runningDurationSeconds = durationSeconds;
				}
			}
			catch (Exception exception)
			{
				LogSamplingException(exception);
			}
			Volatile.Write(ref _current, new SystemResourceUsageInfo(isCpuAvailable ? Math.Clamp(cpuRate, 0, 1) : 0, isCpuAvailable,
				isMemoryAvailable ? Math.Clamp(memoryRate, 0, 1) : 0, isMemoryAvailable, DateTimeOffset.UtcNow, runningDurationSeconds));
		}
		finally
		{
			Volatile.Write(ref _isSampling, 0);
		}
	}

	#endregion

	////////////////////////////////////////////////
	// @接口实现
	////////////////////////////////////////////////

	#region 接口实现

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
		{
			return;
		}
		using var completed = new ManualResetEvent(false);
		if (_timer.Dispose(completed))
		{
			completed.WaitOne();
		}
		_reader.Dispose();
	}

	#endregion
}
