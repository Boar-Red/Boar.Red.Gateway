using System;
using System.Runtime.InteropServices;

namespace Boar.Red.Gateway.Utils;

internal static class SystemResourceNativeMethods
{
	////////////////////////////////////////////////
	// @静态常量
	////////////////////////////////////////////////

	#region 静态常量

	public const string MacSystemLibrary = "/usr/lib/libSystem.B.dylib";

	#endregion

	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[DllImport("pdh.dll", EntryPoint = "PdhOpenQueryW", CharSet = CharSet.Unicode, ExactSpelling = true)]
	public static extern uint OpenPdhQuery(string? dataSource, UIntPtr userData, out IntPtr query);

	[DllImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", CharSet = CharSet.Unicode, ExactSpelling = true)]
	public static extern uint AddEnglishPdhCounter(IntPtr query, string counterPath, UIntPtr userData, out IntPtr counter);

	[DllImport("pdh.dll", EntryPoint = "PdhCollectQueryData", ExactSpelling = true)]
	public static extern uint CollectPdhQueryData(IntPtr query);

	[DllImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterValue", ExactSpelling = true)]
	public static extern uint GetFormattedPdhCounterValue(IntPtr counter, uint format, out uint type, out WindowsPdhCounterValue value);

	[DllImport("pdh.dll", EntryPoint = "PdhCloseQuery", ExactSpelling = true)]
	public static extern uint ClosePdhQuery(IntPtr query);

	[DllImport("kernel32.dll", EntryPoint = "GlobalMemoryStatusEx", ExactSpelling = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetGlobalMemoryStatus(ref WindowsMemoryStatus status);

	[DllImport(MacSystemLibrary, EntryPoint = "mach_host_self")]
	public static extern uint GetMachHost();

	[DllImport(MacSystemLibrary, EntryPoint = "host_statistics")]
	public static extern int GetMachHostStatistics(uint host, int flavor, [Out] uint[] values, ref uint count);

	[DllImport(MacSystemLibrary, EntryPoint = "host_page_size")]
	public static extern int GetMachHostPageSize(uint host, out UIntPtr pageSize);

	[DllImport(MacSystemLibrary, EntryPoint = "sysctlbyname", CharSet = CharSet.Ansi)]
	public static extern int GetSysctlValue(string name, out ulong value, ref UIntPtr valueLength, IntPtr newValue, UIntPtr newValueLength);

	[DllImport(MacSystemLibrary, EntryPoint = "mach_port_deallocate")]
	public static extern int DeallocateMachPort(uint task, uint port);

	#endregion
}
