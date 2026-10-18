using BaoXia.Utils;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Service.Attributes;
using Boar.Red.Service.BrService.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Services;

[Service]
public class BlockRecordConfigAutoSaveService : IServiceLifeCycleEvents
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	private DateTimeOffset? _blockRecordConfigSaveTime = null;
	private readonly LoopTask _taskToSaveBlockRecordConfig;

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public BlockRecordConfigAutoSaveService()
	{
		_taskToSaveBlockRecordConfig = new((cancellationToken) =>
		{
			var blockRecordConfig = Config.BlockRecord;
			if (blockRecordConfig == null)
			{
				return true;
			}
			var blockRecordsUpdateTime = blockRecordConfig.BlockRecordsUpdateTime;
			if (blockRecordsUpdateTime == null)
			{
				return true;
			}
			if (_blockRecordConfigSaveTime != null
				&& _blockRecordConfigSaveTime.Value >= blockRecordsUpdateTime.Value)
			{
				return true;
			}

			// !!!
			lock (blockRecordConfig.SyncRoot)
			{
				if (blockRecordConfig.Save())
				{
					_blockRecordConfigSaveTime = blockRecordsUpdateTime;
				}
			}
			// !!!
			return true;
		}, 1.0, false);
	}

	#endregion


	////////////////////////////////////////////////
	// @实现“IServiceLifeCycleEvents”
	////////////////////////////////////////////////

	#region 实现“IServiceLifeCycleEvents”

	public int ServiceStartupSequenceNumber => 0;

	public async Task DidServiceBootingAsync(Action<double> toReceiveBootingProgress, CancellationToken cancellationToken)
	{
		await Task.CompletedTask;
	}

	public async Task DidServiceStartedAsync(CancellationToken cancellationToken)
	{
		// !!!
		_taskToSaveBlockRecordConfig.Start();
		// !!!
		await Task.CompletedTask;
	}

	public async Task DidServiceShutdownAsync(Action<double> toReceiveShutdownProgress, CancellationToken cancellationToken)
	{
		await Task.CompletedTask;
	}

	public void DidApplicationNormalExit(CancellationToken cancellationToken)
	{ }

	#endregion
}