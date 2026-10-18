using Boar.Red.Gateway.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test.Utils;

[TestClass]
public class GatewayRestartServiceTest
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[TestMethod]
	public void RestartCommand_DotNetHostKeepsAssemblyAndArgumentsAndReplacesOldParentId()
	{
		var info = GatewayRestartService.CreateRestartStartInfo(@"C:\runtime\dotnet.exe",
			[@"D:\gateway folder\gateway.dll", "--urls", "http://127.0.0.1:8080", GatewayRestartService.RestartParentProcessArgument, "123"], 456, @"D:\gateway folder");
		CollectionAssert.AreEqual(new[] { @"D:\gateway folder\gateway.dll", "--urls", "http://127.0.0.1:8080", GatewayRestartService.RestartParentProcessArgument, "456" }, info.ArgumentList.ToArray());
		Assert.IsFalse(info.UseShellExecute);
		Assert.IsTrue(info.CreateNoWindow);
		Assert.AreEqual(@"D:\gateway folder", info.WorkingDirectory);
	}

	[TestMethod]
	public void RestartCommand_AppHostDoesNotDuplicateExecutableArgument()
	{
		var info = GatewayRestartService.CreateRestartStartInfo(@"D:\gateway.exe", [@"D:\gateway.dll", "--environment", "Development"], 456, @"D:\gateway");
		CollectionAssert.AreEqual(new[] { "--environment", "Development", GatewayRestartService.RestartParentProcessArgument, "456" }, info.ArgumentList.ToArray());
	}

	[TestMethod]
	public async Task Startup_WithoutRestartFlagPreservesArgumentsAndInvalidParentIsRejected()
	{
		CollectionAssert.AreEqual(new[] { "--environment", "Development" }, await GatewayRestartService.WaitForParentExitAsync(["--environment", "Development"]));
		await Assert.ThrowsAsync<ArgumentException>(() => GatewayRestartService.WaitForParentExitAsync([GatewayRestartService.RestartParentProcessArgument]));
		await Assert.ThrowsAsync<ArgumentException>(() => GatewayRestartService.WaitForParentExitAsync([GatewayRestartService.RestartParentProcessArgument, Environment.ProcessId.ToString()]));
	}

	#endregion
}
