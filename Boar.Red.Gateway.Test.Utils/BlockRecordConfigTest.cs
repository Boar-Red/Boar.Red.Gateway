using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Defines.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test.Utils;

[TestClass]
public class BlockRecordConfigTest
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	private static IpAddressBlockRecord CreateRecord(string ipAddress, string source = "manual")
	{
		return new IpAddressBlockRecord()
		{
			TargetIpAddress = ipAddress,
			BlockSourceName = source,
			BlockBeginTime = DateTimeOffset.UtcNow.AddMinutes(-1),
			BlockEndTime = DateTimeOffset.UtcNow.AddDays(1),
			BlockRemark = "封禁测试"
		};
	}

	[TestMethod]
	public void Update_ChangesIpAndIndexWithoutMutatingOriginalSnapshot()
	{
		var original = CreateRecord("192.0.2.1");
		var config = new BlockRecordConfig() { BlockRecords = [original] };
		var updated = CreateRecord("192.0.2.2");
		updated.BlockRemark = "修改后的描述";
		Assert.IsTrue(config.TryUpdateBlockRecord("192.0.2.1", "manual", updated, out var error));
		Assert.IsNull(error);
		Assert.HasCount(1, config.BlockRecords!);
		Assert.AreEqual("192.0.2.1", original.TargetIpAddress);
		Assert.AreEqual("修改后的描述", config.BlockRecords![0].BlockRemark);
		Assert.IsFalse(config.IsIpBlocked("192.0.2.1", DateTimeOffset.UtcNow));
		Assert.IsTrue(config.IsIpBlocked("192.0.2.2", DateTimeOffset.UtcNow));
	}

	[TestMethod]
	public void Update_PreservesOtherSourcesAndSupportsUnchangedIp()
	{
		var other = CreateRecord("192.0.2.1", "other");
		var config = new BlockRecordConfig() { BlockRecords = [CreateRecord("192.0.2.1"), other] };
		var updated = CreateRecord("192.0.2.1");
		updated.BlockRemark = "仅修改描述";
		Assert.IsTrue(config.TryUpdateBlockRecord("192.0.2.1", "manual", updated, out _));
		Assert.HasCount(2, config.BlockRecords!);
		Assert.AreSame(other, config.BlockRecords![1]);
		Assert.IsTrue(config.TryUpdateBlockRecord("192.0.2.1", "manual", CreateRecord("192.0.2.2"), out _));
		Assert.IsTrue(config.IsIpBlocked("192.0.2.1", DateTimeOffset.UtcNow));
		Assert.AreSame(other, config.IpAddressToBlockRecords!["192.0.2.1"][0]);
	}

	[TestMethod]
	public void Update_RejectsCollisionAndMissingOriginalWithoutPartialChanges()
	{
		var original = CreateRecord("192.0.2.1");
		var existing = CreateRecord("192.0.2.2");
		var config = new BlockRecordConfig() { BlockRecords = [original, existing] };
		var snapshot = config.BlockRecords;
		Assert.IsFalse(config.TryUpdateBlockRecord("192.0.2.1", "manual", CreateRecord("192.0.2.2"), out var conflict));
		Assert.IsTrue(conflict?.Contains("相同来源") == true);
		Assert.AreSame(snapshot, config.BlockRecords);
		Assert.IsFalse(config.TryUpdateBlockRecord("192.0.2.3", "manual", CreateRecord("192.0.2.4"), out var missing));
		Assert.IsTrue(missing?.Contains("不存在") == true);
		Assert.AreSame(snapshot, config.BlockRecords);
		Assert.IsFalse(config.TryUpdateBlockRecord("192.0.2.1", "manual", CreateRecord("192.0.2.1", "other"), out _));
		Assert.AreSame(snapshot, config.BlockRecords);
	}

	[TestMethod]
	public void Update_AndConcurrentAddsRetainEveryOtherRecord()
	{
		var config = new BlockRecordConfig() { BlockRecords = [CreateRecord("192.0.2.1")] };
		Parallel.For(0, 20, index =>
		{
			if (index == 0)
			{
				Assert.IsTrue(config.TryUpdateBlockRecord("192.0.2.1", "manual", CreateRecord("192.0.2.255"), out _));
			}
			else
			{
				config.AddBlockRecords([CreateRecord("192.0.2." + (index + 1))]);
			}
		});
		Assert.HasCount(20, config.BlockRecords!);
		Assert.AreEqual(20, config.BlockRecords!.Select(record => record.TargetIpAddress).Distinct().Count());
	}

	#endregion
}
