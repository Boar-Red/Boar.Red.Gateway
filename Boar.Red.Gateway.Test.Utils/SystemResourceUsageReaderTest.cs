using Boar.Red.Gateway.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test.Utils;

[TestClass]
public class SystemResourceUsageReaderTest
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[TestMethod]
	public void CalculateCpuUsageRate_UsesIntervalRatherThanUptime()
	{
		Assert.IsTrue(SystemResourceUsageReader.TryCalculateCpuUsageRate(100, 60, 200, 130, out var rate));
		Assert.AreEqual(0.3, rate, 0.000001);
	}

	[TestMethod]
	public void CalculateCpuUsageRate_RejectsCounterResetsAndInvalidDeltas()
	{
		foreach (var counters in new (ulong Total, ulong Idle)[] { (100, 60), (90, 60), (110, 59), (110, 80), (110, 120) })
		{
			Assert.IsFalse(SystemResourceUsageReader.TryCalculateCpuUsageRate(100, 60, counters.Total, counters.Idle, out var rate));
			Assert.AreEqual(0, rate);
		}
	}

	[TestMethod]
	public void ParseLinuxCpuTimes_ExcludesGuestCountersAndTreatsIoWaitAsIdle()
	{
		Assert.IsTrue(SystemResourceUsageReader.TryParseLinuxCpuTimes("cpu  100 20 30 500 40 10 5 5 90 10", out var total, out var idle));
		Assert.AreEqual(710UL, total);
		Assert.AreEqual(540UL, idle);
		Assert.IsTrue(SystemResourceUsageReader.TryParseLinuxCpuTimes("cpu\t10 2 3 20", out total, out idle));
		Assert.AreEqual(35UL, total);
		Assert.AreEqual(20UL, idle);
	}

	[TestMethod]
	[DataRow(null)]
	[DataRow("cpu0 10 2 3 20")]
	[DataRow("cpu 10 2 3")]
	[DataRow("cpu 10 -2 3 20")]
	[DataRow("cpu 18446744073709551615 1 0 0")]
	public void ParseLinuxCpuTimes_RejectsMalformedOrOverflowingCounters(string? line)
	{
		Assert.IsFalse(SystemResourceUsageReader.TryParseLinuxCpuTimes(line, out _, out _));
	}

	[TestMethod]
	public void ReadLinuxMemoryUsageRate_UsesAvailableMemoryIncludingReclaimableCache()
	{
		using var input = new StringReader("MemTotal: 1000 kB\nMemFree: 50 kB\nCached: 300 kB\nMemAvailable: 400 kB\n");
		Assert.IsTrue(SystemResourceUsageReader.TryReadLinuxMemoryUsageRate(input, out var rate));
		Assert.AreEqual(0.6, rate, 0.000001);
	}

	[TestMethod]
	[DataRow("MemTotal: 1000 kB\nMemFree: 50 kB\n")]
	[DataRow("MemTotal: 0 kB\nMemAvailable: 0 kB\n")]
	[DataRow("MemTotal: 1000 kB\nMemAvailable: -1 kB\n")]
	[DataRow("MemTotal: 1000 kB\nMemAvailable: 400 MB\n")]
	public void ReadLinuxMemoryUsageRate_RejectsUnavailableOrInvalidValues(string content)
	{
		using var input = new StringReader(content);
		Assert.IsFalse(SystemResourceUsageReader.TryReadLinuxMemoryUsageRate(input, out var rate));
		Assert.AreEqual(0, rate);
	}

	[TestMethod]
	public void ReadLinuxMemoryUsageRate_ClampsChangesBetweenMemoryCounters()
	{
		using var input = new StringReader("MemAvailable: 1001 kB\nMemTotal: 1000 kB\n");
		Assert.IsTrue(SystemResourceUsageReader.TryReadLinuxMemoryUsageRate(input, out var rate));
		Assert.AreEqual(0, rate);
	}

	[TestMethod]
	public async Task ReadRunningDuration_IncludesTimeBeforeFirstSampleAndContinuesIncreasingAsync()
	{
		using var process = Process.GetCurrentProcess();
		using var reader = new SystemResourceUsageReader();
		var expectedDurationSeconds = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds;
		Assert.IsTrue(reader.TryReadSystemRunningDurationSeconds(out var firstDurationSeconds));
		Assert.AreEqual(expectedDurationSeconds, firstDurationSeconds, 2.0);
		await Task.Delay(50);
		Assert.IsTrue(reader.TryReadSystemRunningDurationSeconds(out var secondDurationSeconds));
		Assert.IsTrue(secondDurationSeconds > firstDurationSeconds);
	}

	[TestMethod]
	public async Task ReadNativeResources_ReturnsPhysicalMemoryAndCpuIntervalAsync()
	{
		if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
		{
			Assert.Inconclusive("当前平台未提供整机资源采集实现。");
		}
		using var reader = new SystemResourceUsageReader();
		Assert.IsFalse(reader.TryReadCpuUsageRate(out _), "首次 CPU 采样需要建立时间差值基线。");
		Assert.IsTrue(reader.TryReadMemoryUsageRate(out var memoryRate));
		Assert.IsTrue(double.IsFinite(memoryRate) && memoryRate >= 0 && memoryRate <= 1);
		await Task.Delay(1100);
		Assert.IsTrue(reader.TryReadCpuUsageRate(out var cpuRate));
		Assert.IsTrue(double.IsFinite(cpuRate) && cpuRate >= 0 && cpuRate <= 1);
	}

	#endregion
}
