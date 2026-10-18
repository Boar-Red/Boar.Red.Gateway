using System.Runtime.InteropServices;

namespace Boar.Red.Gateway.Utils;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsMemoryStatus
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public uint Length;
	public uint MemoryLoad;
	public ulong TotalPhysical;
	public ulong AvailablePhysical;
	public ulong TotalPageFile;
	public ulong AvailablePageFile;
	public ulong TotalVirtual;
	public ulong AvailableVirtual;
	public ulong AvailableExtendedVirtual;

	#endregion
}
