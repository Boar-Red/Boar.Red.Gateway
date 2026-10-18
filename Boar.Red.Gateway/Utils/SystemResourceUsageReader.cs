using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Boar.Red.Gateway.Utils;

/// <summary>整机资源采集，仅供单个后台采样线程调用；Linux 容器中使用 /proc 的系统视图。</summary>
public sealed class SystemResourceUsageReader : ISystemResourceUsageReader
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	private IntPtr _windowsCpuQuery;
	private IntPtr _windowsCpuCounter;
	private bool _hasWindowsCpuSample;
	private bool _hasCpuTimes;
	private ulong _lastCpuTotal;
	private ulong _lastCpuIdle;
	private uint _macHost;
	private uint _macTask;
	private readonly uint[] _macCpuTicks = new uint[4];
	private readonly uint[] _macMemoryPages = new uint[15];
	private double _processRunningDurationInFirstSampleSeconds;
	private long? _processRunningDurationFirstSampleTimestamp;
	private bool _isDisposed;

	#endregion

	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	private void CloseWindowsCpuQuery()
	{
		if (_windowsCpuQuery != IntPtr.Zero)
		{
			SystemResourceNativeMethods.ClosePdhQuery(_windowsCpuQuery);
			_windowsCpuQuery = IntPtr.Zero;
		}
		_windowsCpuCounter = IntPtr.Zero;
		_hasWindowsCpuSample = false;
	}

	private bool TryReadWindowsCpuUsageRate(out double usageRate)
	{
		usageRate = 0;
		if (_windowsCpuQuery == IntPtr.Zero)
		{
			if (SystemResourceNativeMethods.OpenPdhQuery(null, UIntPtr.Zero, out _windowsCpuQuery) != 0)
			{
				return false;
			}
			// 使用英文计数器名，支持不同系统语言；Processor Information 包括全部处理器组。
			// https://learn.microsoft.com/en-us/windows/win32/api/pdh/nf-pdh-pdhaddenglishcounterw
			if (SystemResourceNativeMethods.AddEnglishPdhCounter(_windowsCpuQuery, @"\Processor Information(_Total)\% Processor Time", UIntPtr.Zero, out _windowsCpuCounter) != 0)
			{
				CloseWindowsCpuQuery();
				return false;
			}
		}
		if (SystemResourceNativeMethods.CollectPdhQueryData(_windowsCpuQuery) != 0)
		{
			CloseWindowsCpuQuery();
			return false;
		}
		if (!_hasWindowsCpuSample)
		{
			_hasWindowsCpuSample = true;
			return false;
		}
		const uint pdhFormatDouble = 0x00000200;
		if (SystemResourceNativeMethods.GetFormattedPdhCounterValue(_windowsCpuCounter, pdhFormatDouble, out _, out var value) != 0
			|| value.Status > 1 || !double.IsFinite(value.Value))
		{
			return false;
		}
		usageRate = Math.Clamp(value.Value / 100, 0, 1);
		return true;
	}

	private bool TryGetMacHost()
	{
		if (_macHost != 0)
		{
			return true;
		}
		// mach_task_self() 是头文件宏，读取导出的任务端口变量用于释放 host send right。
		var library = NativeLibrary.Load(SystemResourceNativeMethods.MacSystemLibrary);
		try
		{
			_macTask = unchecked((uint)Marshal.ReadInt32(NativeLibrary.GetExport(library, "mach_task_self_")));
		}
		finally
		{
			NativeLibrary.Free(library);
		}
		_macHost = SystemResourceNativeMethods.GetMachHost();
		return _macHost != 0;
	}

	private bool TryReadCpuTimes(ulong total, ulong idle, out double usageRate)
	{
		usageRate = 0;
		var isAvailable = _hasCpuTimes && TryCalculateCpuUsageRate(_lastCpuTotal, _lastCpuIdle, total, idle, out usageRate);
		_lastCpuTotal = total;
		_lastCpuIdle = idle;
		_hasCpuTimes = true;
		return isAvailable;
	}

	private bool TryReadMacMemoryUsageRate(out double usageRate)
	{
		usageRate = 0;
		var memorySizeLength = (UIntPtr)sizeof(ulong);
		var count = (uint)_macMemoryPages.Length;
		if (!TryGetMacHost() || SystemResourceNativeMethods.GetSysctlValue("hw.memsize", out var totalBytes, ref memorySizeLength, IntPtr.Zero, UIntPtr.Zero) != 0
			|| totalBytes == 0 || SystemResourceNativeMethods.GetMachHostPageSize(_macHost, out var pageSize) != 0 || pageSize == UIntPtr.Zero
			|| SystemResourceNativeMethods.GetMachHostStatistics(_macHost, 2, _macMemoryPages, ref count) != 0 || count < 3)
		{
			return false;
		}
		// 以 free + inactive 作为可回收内存估计；free 已包含 speculative，避免重复相加。
		// https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/vm_statistics.h
		var availableBytes = ((ulong)_macMemoryPages[0] + _macMemoryPages[2]) * pageSize.ToUInt64();
		usageRate = 1 - (double)Math.Min(totalBytes, availableBytes) / totalBytes;
		return true;
	}

	public static bool TryCalculateCpuUsageRate(ulong previousTotal, ulong previousIdle, ulong total, ulong idle, out double usageRate)
	{
		usageRate = 0;
		if (total <= previousTotal || idle < previousIdle || idle > total || previousIdle > previousTotal)
		{
			return false;
		}
		var totalDifference = total - previousTotal;
		var idleDifference = idle - previousIdle;
		if (idleDifference > totalDifference)
		{
			return false;
		}
		usageRate = (double)(totalDifference - idleDifference) / totalDifference;
		return true;
	}

	public static bool TryParseLinuxCpuTimes(string? line, out ulong total, out ulong idle)
	{
		total = 0;
		idle = 0;
		var values = line?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
		if (values == null || values.Length < 5 || values[0] != "cpu")
		{
			return false;
		}
		// 只累计前八项，guest 和 guest_nice 已计入 user/nice，不能再次累计。
		for (var index = 1; index < Math.Min(values.Length, 9); index++)
		{
			if (!ulong.TryParse(values[index], NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value > ulong.MaxValue - total)
			{
				return false;
			}
			total += value;
			if (index is 4 or 5)
			{
				idle += value;
			}
		}
		return total > 0;
	}

	public static bool TryReadLinuxMemoryUsageRate(TextReader reader, out double usageRate)
	{
		usageRate = 0;
		ulong? total = null;
		ulong? available = null;
		string? line;
		while ((line = reader.ReadLine()) != null)
		{
			var colonIndex = line.IndexOf(':');
			if (colonIndex < 0)
			{
				continue;
			}
			var name = line.AsSpan(0, colonIndex);
			if (!name.SequenceEqual("MemTotal") && !name.SequenceEqual("MemAvailable"))
			{
				continue;
			}
			var content = line.AsSpan(colonIndex + 1).Trim();
			if (!content.EndsWith("kB", StringComparison.Ordinal) || !ulong.TryParse(content[..^2].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
			{
				return false;
			}
			if (name.SequenceEqual("MemTotal"))
			{
				total = value;
			}
			else
			{
				available = value;
			}
			if (total.HasValue && available.HasValue)
			{
				if (total.Value == 0)
				{
					return false;
				}
				usageRate = 1 - (double)Math.Min(available.Value, total.Value) / total.Value;
				return true;
			}
		}
		return false;
	}

	#endregion

	////////////////////////////////////////////////
	// @接口实现
	////////////////////////////////////////////////

	#region 接口实现

	public bool TryReadCpuUsageRate(out double usageRate)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);
		usageRate = 0;
		if (OperatingSystem.IsWindows())
		{
			return TryReadWindowsCpuUsageRate(out usageRate);
		}
		if (OperatingSystem.IsLinux())
		{
			using var reader = File.OpenText("/proc/stat");
			return TryParseLinuxCpuTimes(reader.ReadLine(), out var total, out var idle) && TryReadCpuTimes(total, idle, out usageRate);
		}
		if (OperatingSystem.IsMacOS() && TryGetMacHost())
		{
			var count = (uint)_macCpuTicks.Length;
			if (SystemResourceNativeMethods.GetMachHostStatistics(_macHost, 3, _macCpuTicks, ref count) == 0 && count == 4)
			{
				return TryReadCpuTimes((ulong)_macCpuTicks[0] + _macCpuTicks[1] + _macCpuTicks[2] + _macCpuTicks[3], _macCpuTicks[2], out usageRate);
			}
		}
		return false;
	}

	public bool TryReadMemoryUsageRate(out double usageRate)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);
		usageRate = 0;
		if (OperatingSystem.IsWindows())
		{
			var status = new WindowsMemoryStatus { Length = (uint)Marshal.SizeOf<WindowsMemoryStatus>() };
			if (SystemResourceNativeMethods.GetGlobalMemoryStatus(ref status) && status.TotalPhysical > 0)
			{
				usageRate = 1 - (double)Math.Min(status.TotalPhysical, status.AvailablePhysical) / status.TotalPhysical;
				return true;
			}
		}
		else if (OperatingSystem.IsLinux())
		{
			using var reader = File.OpenText("/proc/meminfo");
			return TryReadLinuxMemoryUsageRate(reader, out usageRate);
		}
		else if (OperatingSystem.IsMacOS())
		{
			return TryReadMacMemoryUsageRate(out usageRate);
		}
		return false;
	}

	/// <summary>网关进程启动以来的时长。仅首次后台采样读取进程启动时间，后续使用单调时钟累计。</summary>
	public bool TryReadSystemRunningDurationSeconds(out double durationSeconds)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);
		if (!_processRunningDurationFirstSampleTimestamp.HasValue)
		{
			using var process = Process.GetCurrentProcess();
			var startTime = process.StartTime.ToUniversalTime();
			_processRunningDurationInFirstSampleSeconds = Math.Max(0, (DateTime.UtcNow - startTime).TotalSeconds);
			_processRunningDurationFirstSampleTimestamp = Stopwatch.GetTimestamp();
		}
		durationSeconds = _processRunningDurationInFirstSampleSeconds + Stopwatch.GetElapsedTime(_processRunningDurationFirstSampleTimestamp.Value).TotalSeconds;
		return double.IsFinite(durationSeconds) && durationSeconds >= 0;
	}

	public void Dispose()
	{
		if (_isDisposed)
		{
			return;
		}
		_isDisposed = true;
		CloseWindowsCpuQuery();
		if (_macHost != 0)
		{
			SystemResourceNativeMethods.DeallocateMachPort(_macTask, _macHost);
			_macHost = 0;
		}
	}

	#endregion
}
