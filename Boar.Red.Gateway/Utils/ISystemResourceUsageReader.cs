using System;

namespace Boar.Red.Gateway.Utils;

public interface ISystemResourceUsageReader : IDisposable
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	bool TryReadCpuUsageRate(out double usageRate);
	bool TryReadMemoryUsageRate(out double usageRate);
	bool TryReadSystemRunningDurationSeconds(out double durationSeconds);

	#endregion
}
