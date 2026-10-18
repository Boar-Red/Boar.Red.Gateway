using System.Runtime.InteropServices;

namespace Boar.Red.Gateway.Utils;

[StructLayout(LayoutKind.Explicit, Size = 16)]
internal struct WindowsPdhCounterValue
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	[FieldOffset(0)]
	public uint Status;

	[FieldOffset(8)]
	public double Value;

	#endregion
}
