using BaoXia.Utils;
using Boar.Red.Gateway.ConfigFiles;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test.Utils;

public class RouterConfigForConfigApiTest : RouterConfig
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

	#endregion

	////////////////////////////////////////////////
	// @重载
	////////////////////////////////////////////////

	#region 重载

	protected override Task DidLoadConfigFileCompletedFromFilePathAsync(string filePath, ConfigFile configFile)
	{
		return Task.CompletedTask;
	}

	#endregion
}
