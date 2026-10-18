using Boar.Red.Gateway.Extensions;
using Boar.Red.Gateway.Models;
using Boar.Red.Gateway.Utils;
using Boar.Red.Gateway.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test.Utils;

[TestClass]
[DoNotParallelize]
public class SystemResourceUsageMonitorTest
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[TestMethod]
	public async Task Current_DoesNotWaitForSamplingOrStartOverlappingSamplesAsync()
	{
		using var entered = new ManualResetEventSlim();
		using var release = new ManualResetEventSlim();
		var reader = new SystemResourceUsageReaderForTest
		{
			WillReadCpu = () =>
			{
				entered.Set();
				release.Wait(TimeSpan.FromSeconds(10));
			}
		};
		var monitor = new SystemResourceUsageMonitor(reader);
		try
		{
			Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
			var reads = Task.Run(() =>
			{
				var snapshot = monitor.Current;
				Assert.IsFalse(snapshot.IsCpuUsageRateAvailable);
				Assert.IsFalse(snapshot.IsMemoryUsageRateAvailable);
				Assert.IsNull(snapshot.SampleTime);
				Assert.IsNull(snapshot.SystemRunningDurationSeconds);
				for (var index = 0; index < 10000; index++)
				{
					Assert.AreSame(snapshot, monitor.Current);
				}
			});
			await reads.WaitAsync(TimeSpan.FromSeconds(1));
			await Task.Delay(1500);
			Assert.AreEqual(1, reader.CpuReadCount, "慢采样期间应跳过下一次定时回调。");
			Assert.AreEqual(0, reader.MemoryReadCount);
			Assert.AreEqual(0, reader.RunningDurationReadCount);
			release.Set();
			Assert.IsTrue(SpinWait.SpinUntil(() => monitor.Current.SampleTime.HasValue, TimeSpan.FromSeconds(5)));
			Assert.AreEqual(0.25, monitor.Current.CpuUsageRate);
			Assert.AreEqual(0.6, monitor.Current.MemoryUsageRate);
			Assert.AreEqual(86400.5, monitor.Current.SystemRunningDurationSeconds);
		}
		finally
		{
			release.Set();
			monitor.Dispose();
		}
		Assert.IsTrue(reader.IsDisposed);
	}

	[TestMethod]
	[DataRow(true)]
	[DataRow(false)]
	public void SamplingFailure_DoesNotInvalidateOtherMetric(bool isCpuFailure)
	{
		var reader = new SystemResourceUsageReaderForTest
		{
			CpuException = isCpuFailure ? new IOException("CPU read failed") : null,
			MemoryException = isCpuFailure ? null : new IOException("Memory read failed")
		};
		using var monitor = new SystemResourceUsageMonitor(reader);
		Assert.IsTrue(SpinWait.SpinUntil(() => monitor.Current.SampleTime.HasValue, TimeSpan.FromSeconds(5)));
		var snapshot = monitor.Current;
		Assert.AreEqual(!isCpuFailure, snapshot.IsCpuUsageRateAvailable);
		Assert.AreEqual(isCpuFailure, snapshot.IsMemoryUsageRateAvailable);
		Assert.AreEqual(isCpuFailure ? 0 : 0.25, snapshot.CpuUsageRate);
		Assert.AreEqual(isCpuFailure ? 0.6 : 0, snapshot.MemoryUsageRate);
		Assert.AreEqual(86400.5, snapshot.SystemRunningDurationSeconds);
	}

	[TestMethod]
	public void Sampling_RejectsNonFiniteValuesAndHonorsAvailability()
	{
		var reader = new SystemResourceUsageReaderForTest { CpuUsageRate = double.NaN, IsMemoryAvailable = false };
		using var monitor = new SystemResourceUsageMonitor(reader);
		Assert.IsTrue(SpinWait.SpinUntil(() => monitor.Current.SampleTime.HasValue, TimeSpan.FromSeconds(5)));
		var snapshot = monitor.Current;
		Assert.IsFalse(snapshot.IsCpuUsageRateAvailable);
		Assert.IsFalse(snapshot.IsMemoryUsageRateAvailable);
		Assert.AreEqual(0, snapshot.CpuUsageRate);
		Assert.AreEqual(0, snapshot.MemoryUsageRate);
	}

	[TestMethod]
	public void RunningDurationFailure_DoesNotInvalidateResourceUsage()
	{
		var reader = new SystemResourceUsageReaderForTest { RunningDurationException = new IOException("Duration read failed") };
		using var monitor = new SystemResourceUsageMonitor(reader);
		Assert.IsTrue(SpinWait.SpinUntil(() => monitor.Current.SampleTime.HasValue, TimeSpan.FromSeconds(5)));
		var snapshot = monitor.Current;
		Assert.IsNull(snapshot.SystemRunningDurationSeconds);
		Assert.IsTrue(snapshot.IsCpuUsageRateAvailable);
		Assert.IsTrue(snapshot.IsMemoryUsageRateAvailable);
		Assert.AreEqual(0.25, snapshot.CpuUsageRate);
		Assert.AreEqual(0.6, snapshot.MemoryUsageRate);
	}

	[TestMethod]
	[DataRow(double.NaN, true)]
	[DataRow(double.PositiveInfinity, true)]
	[DataRow(-1.0, true)]
	[DataRow(86400.5, false)]
	public void RunningDuration_RejectsInvalidOrUnavailableValues(double durationSeconds, bool isAvailable)
	{
		var reader = new SystemResourceUsageReaderForTest { RunningDurationSeconds = durationSeconds, IsRunningDurationAvailable = isAvailable };
		using var monitor = new SystemResourceUsageMonitor(reader);
		Assert.IsTrue(SpinWait.SpinUntil(() => monitor.Current.SampleTime.HasValue, TimeSpan.FromSeconds(5)));
		Assert.IsNull(monitor.Current.SystemRunningDurationSeconds);
	}

	[TestMethod]
	public void ToResponse_PreservesMetricAvailabilityAndSampleTime()
	{
		var sampleTime = DateTimeOffset.UtcNow;
		var snapshot = new SystemResourceUsageInfo(0, false, 0.6, true, sampleTime, 86400.5);
		var response = new GetRouteStatisticsInfesResponse { RouterName = "All" };
		Assert.AreSame(response, snapshot.ToGetRouteStatisticsInfesResponse(response));
		Assert.AreEqual("All", response.RouterName);
		Assert.AreEqual(0, response.SystemCpuUsageRate);
		Assert.IsFalse(response.IsSystemCpuUsageRateAvailable);
		Assert.AreEqual(0.6, response.SystemMemoryUsageRate);
		Assert.IsTrue(response.IsSystemMemoryUsageRateAvailable);
		Assert.AreEqual(sampleTime, response.SystemResourceUsageSampleTime);
		Assert.AreEqual(86400.5, response.SystemRunningDurationSeconds);
	}

	#endregion
}
