using Boar.Red.Gateway.Models;
using Boar.Red.Gateway.ViewModels;

namespace Boar.Red.Gateway.Extensions;

public static class SystemResourceUsageInfoFromExtension
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public static GetRouteStatisticsInfesResponse ToGetRouteStatisticsInfesResponse(this SystemResourceUsageInfo info, GetRouteStatisticsInfesResponse response)
	{
		response.SystemCpuUsageRate = info.CpuUsageRate;
		response.SystemMemoryUsageRate = info.MemoryUsageRate;
		response.IsSystemCpuUsageRateAvailable = info.IsCpuUsageRateAvailable;
		response.IsSystemMemoryUsageRateAvailable = info.IsMemoryUsageRateAvailable;
		response.SystemResourceUsageSampleTime = info.SampleTime;
		response.SystemRunningDurationSeconds = info.SystemRunningDurationSeconds;
		return response;
	}

	#endregion
}
