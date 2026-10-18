using Boar.Red.Gateway.ConfigFiles;
using System.IO;

namespace Boar.Red.Gateway.Test.Utils;

public class BlockRecordConfigForConfigApiTest : BlockRecordConfig
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public void DisableFileWatcher()
	{
		_fileWatcher?.Dispose();
		_fileWatcher = null;
	}

	public void NotifyFileChanged()
	{
		DidConfigFileChanged(this, new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(FilePath)!, Path.GetFileName(FilePath)));
	}

	#endregion
}
