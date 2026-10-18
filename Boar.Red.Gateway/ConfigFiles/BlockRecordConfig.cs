using BaoXia.Utils;
using BaoXia.Utils.Dictionaries;
using BaoXia.Utils.Extensions;
using Boar.Red.Gateway.Defines.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Boar.Red.Gateway.ConfigFiles;

public class BlockRecordConfig : ConfigFile
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	private IpAddressBlockRecord[]? _blockRecords = null;

	private readonly object _blockRecordsLock = new();

	private DateTime? _fileWriteTimeUtcSelfSaved;

	[JsonIgnore]
	public object SyncRoot => _blockRecordsLock;

	public IpAddressBlockRecord[]? BlockRecords
	{
		get
		{
			return _blockRecords;
		}
		set
		{
			lock (_blockRecordsLock)
			{

				_blockRecords = value;
				BlockRecordsUpdateTime = DateTimeOffset.Now;

				ConcurrentDictionary<string, List<IpAddressBlockRecord>>? ipAddressToBlackRecordList = null;
				//ConcurrentDictionaryWith2Keys<string, string, IpAddressBlockRecord>? ipAddressAndBlockSourceNameToToBlockRecords = null;
				if (_blockRecords != null)
				{
					foreach (var blockRecord in _blockRecords)
					{
						var targetIpAddress = blockRecord.TargetIpAddress;
						if (string.IsNullOrWhiteSpace(targetIpAddress))
						{
							continue;
						}
						var blockSourceName = blockRecord.BlockSourceName;
						if (string.IsNullOrWhiteSpace(blockSourceName))
						{
							continue;
						}

						ipAddressToBlackRecordList ??= [];
						ipAddressToBlackRecordList.AddOrUpdate(targetIpAddress, [blockRecord], (ipAddress, existedBlockRecords) =>
						{
							existedBlockRecords.Add(blockRecord);
							{ }
							return existedBlockRecords;
						});

						// !!!
						//ipAddressAndBlockSourceNameToToBlockRecords ??= new();
						//ipAddressAndBlockSourceNameToToBlockRecords.Add(targetIpAddress, blockSourceName, blockRecord);
						// !!!
					}
				}
				ConcurrentDictionary<string, IpAddressBlockRecord[]>? ipAddressToBlockRecords = null;
				if (ipAddressToBlackRecordList != null)
				{
					ipAddressToBlockRecords = [];
					foreach (var ipAddressToBlackRecordKeyValue in ipAddressToBlackRecordList)
					{
						ipAddressToBlockRecords.AddOrUpdateWithNewValue(
							ipAddressToBlackRecordKeyValue.Key, [.. ipAddressToBlackRecordKeyValue.Value]);
					}
				}
				// !!!
				IpAddressToBlockRecords = ipAddressToBlockRecords;
				//IpAddressAndBlockSourceNameToToBlockRecords = ipAddressAndBlockSourceNameToToBlockRecords;
				// !!!
			}
		}
	}

	public DateTimeOffset? BlockRecordsUpdateTime { get; set; }

	[JsonIgnore]
	public ConcurrentDictionary<string, IpAddressBlockRecord[]>? IpAddressToBlockRecords { get; set; }

	//public ConcurrentDictionaryWith2Keys<string, string, IpAddressBlockRecord>? IpAddressAndBlockSourceNameToToBlockRecords { get; set; }

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	private void AddBlockRecordsCore(IEnumerable<IpAddressBlockRecord> blockRecordsNeedAdd)
	{
		if (blockRecordsNeedAdd.IsEmpty())
		{
			return;
		}

		var newBlockRecords = new List<IpAddressBlockRecord>();
		var blockSourceNameAndIpAddressToNewBlockRecords
			= new ConcurrentDictionaryWith2Keys<string, string, IpAddressBlockRecord>();
		var lastBlockRecords = BlockRecords;
		if (lastBlockRecords?.Length > 0)
		{
			foreach (var lastBlockRecord in lastBlockRecords)
			{
				var blockSourceName = lastBlockRecord.BlockSourceName;
				if (string.IsNullOrWhiteSpace(blockSourceName))
				{
					continue;
				}
				var targetIpAddress = lastBlockRecord.TargetIpAddress;
				if (string.IsNullOrWhiteSpace(targetIpAddress))
				{
					continue;
				}
				newBlockRecords.Add(lastBlockRecord);
				blockSourceNameAndIpAddressToNewBlockRecords.Add(
					blockSourceName, targetIpAddress, lastBlockRecord);
			}
		}
		foreach (var blockRecordNeedAdd in blockRecordsNeedAdd)
		{
			var blockSourceName = blockRecordNeedAdd.BlockSourceName;
			if (string.IsNullOrWhiteSpace(blockSourceName))
			{
				continue;
			}
			var targetIpAddress = blockRecordNeedAdd.TargetIpAddress;
			if (string.IsNullOrWhiteSpace(targetIpAddress))
			{
				continue;
			}

			if (blockSourceNameAndIpAddressToNewBlockRecords.TryGet(blockSourceName, targetIpAddress, out var blockRecordExisted)
				&& blockRecordExisted != null)
			{
				// !!!
				blockRecordExisted.SetIpAddressBlockRecord(blockRecordNeedAdd);
				// !!!
			}
			else
			{
				// !!!
				newBlockRecords.Add(blockRecordNeedAdd);
				blockSourceNameAndIpAddressToNewBlockRecords.Add(
					blockSourceName, targetIpAddress, blockRecordNeedAdd);
				// !!!
			}
		}
		// !!!
		BlockRecords = [.. newBlockRecords];
		// !!!
	}

	public void AddBlockRecords(IEnumerable<IpAddressBlockRecord> blockRecordsNeedAdd)
	{
		lock (_blockRecordsLock)
		{
			AddBlockRecordsCore(blockRecordsNeedAdd);
		}
	}

	public bool TryUpdateBlockRecord(string originalTargetIpAddress, string originalBlockSourceName,
		IpAddressBlockRecord blockRecord, out string? errorDescription)
	{
		lock (_blockRecordsLock)
		{
			errorDescription = null;
			if (string.IsNullOrWhiteSpace(blockRecord?.TargetIpAddress)
				|| string.IsNullOrWhiteSpace(originalBlockSourceName)
				|| blockRecord.BlockSourceName != originalBlockSourceName
				|| blockRecord.BlockEndTime <= blockRecord.BlockBeginTime)
			{
				errorDescription = "封禁记录无效，请检查IP地址、封禁来源与时间。";
				return false;
			}
			var lastBlockRecords = BlockRecords;
			var originalIndex = lastBlockRecords == null ? -1 : Array.FindIndex(lastBlockRecords,
				record => record.TargetIpAddress == originalTargetIpAddress && record.BlockSourceName == originalBlockSourceName);
			if (originalIndex < 0 || lastBlockRecords == null)
			{
				errorDescription = "原封禁记录已不存在，请刷新黑名单后重试。";
				return false;
			}
			for (var index = 0; index < lastBlockRecords.Length; index++)
			{
				var record = lastBlockRecords[index];
				if (index != originalIndex && record.TargetIpAddress == blockRecord.TargetIpAddress
					&& record.BlockSourceName == blockRecord.BlockSourceName)
				{
					errorDescription = "目标IP已存在相同来源的封禁记录，请使用其他IP地址。";
					return false;
				}
			}
			var newBlockRecords = (IpAddressBlockRecord[])lastBlockRecords.Clone();
			newBlockRecords[originalIndex] = new IpAddressBlockRecord().SetIpAddressBlockRecord(blockRecord);
			// 通过同一写入锁替换记录并重建IP索引，避免分成删除和添加两个请求。
			BlockRecords = newBlockRecords;
			return true;
		}
	}


	private void RemoveBlockRecordsCore(IEnumerable<IpAddressBlockRecord> blockRecordsNeedRemoved)
	{
		if (blockRecordsNeedRemoved.IsEmpty())
		{
			return;
		}


		var blockSourceNameAndIpAddressToNewBlockRecordsNeedRemoved
			= new ConcurrentDictionaryWith2Keys<string, string, IpAddressBlockRecord>();
		foreach (var blockRecordNeedRemoved in blockRecordsNeedRemoved)
		{
			var blockSourceName = blockRecordNeedRemoved.BlockSourceName;
			if (string.IsNullOrWhiteSpace(blockSourceName))
			{
				continue;
			}
			var targetIpAddress = blockRecordNeedRemoved.TargetIpAddress;
			if (string.IsNullOrWhiteSpace(targetIpAddress))
			{
				continue;
			}
			//
			blockSourceNameAndIpAddressToNewBlockRecordsNeedRemoved.Add(
				blockSourceName, targetIpAddress, blockRecordNeedRemoved);
			//
		}


		var newBlockRecords = new List<IpAddressBlockRecord>();
		var lastBlockRecords = BlockRecords;
		if (lastBlockRecords?.Length > 0)
		{
			foreach (var lastBlockRecord in lastBlockRecords)
			{
				var blockSourceName = lastBlockRecord.BlockSourceName;
				if (string.IsNullOrWhiteSpace(blockSourceName))
				{
					continue;
				}
				var targetIpAddress = lastBlockRecord.TargetIpAddress;
				if (string.IsNullOrWhiteSpace(targetIpAddress))
				{
					continue;
				}
				if (blockSourceNameAndIpAddressToNewBlockRecordsNeedRemoved.TryGet(
					blockSourceName, targetIpAddress, out var blockRecordExisted) != true
					|| blockRecordExisted == null)
				{
					// !!!
					newBlockRecords.Add(lastBlockRecord);
					// !!!
				}
			}
		}
		// !!!
		BlockRecords = [.. newBlockRecords];
		// !!!
	}

	public void RemoveBlockRecords(IEnumerable<IpAddressBlockRecord> blockRecordsNeedRemoved)
	{
		lock (_blockRecordsLock)
		{
			RemoveBlockRecordsCore(blockRecordsNeedRemoved);
		}
	}

	public bool IsIpBlocked(string? ipAddress, DateTimeOffset operateTime)
	{
		var ipAddressToBlockRecords = IpAddressToBlockRecords;
		if (ipAddressToBlockRecords.IsEmpty())
		{
			return false;
		}
		if (string.IsNullOrEmpty(ipAddress))
		{
			return true;
		}
		if (ipAddressToBlockRecords?.TryGetValue(ipAddress, out var blockRecords) != true)
		{
			return false;
		}
		if (blockRecords == null || blockRecords.Length < 1)
		{
			return false;
		}

		foreach (var blockRecord in blockRecords)
		{
			if (operateTime >= blockRecord.BlockBeginTime && operateTime < blockRecord.BlockEndTime)
			{
				return true;
			}
		}
		return false;
	}

	#endregion

	////////////////////////////////////////////////
	// @事件节点
	////////////////////////////////////////////////

	#region 事件节点

	protected override void DidConfigFileChanged(object sender, System.IO.FileSystemEventArgs fileSystemEventArgs)
	{
		lock (_blockRecordsLock)
		{
			// 自身保存已更新内存；忽略对应监听通知，避免覆盖保存后尚未落盘的新封禁记录。
			if (_fileWriteTimeUtcSelfSaved == System.IO.File.GetLastWriteTimeUtc(FinalFilePath!))
			{
				return;
			}
			base.DidConfigFileChanged(sender, fileSystemEventArgs);
		}
	}

	internal void DidSaveConfigFileToPath(string filePath)
	{
		_fileWriteTimeUtcSelfSaved = System.IO.File.GetLastWriteTimeUtc(filePath);
	}

	protected override void DidSaveConfigFileCompletedToFilePath(string filePath, ConfigFile configFile)
	{
		DidSaveConfigFileToPath(filePath);
		base.DidSaveConfigFileCompletedToFilePath(filePath, configFile);
	}

	#endregion
}
