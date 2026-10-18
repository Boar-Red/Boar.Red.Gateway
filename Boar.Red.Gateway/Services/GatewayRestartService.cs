using Boar.Red.Gateway.LogFiles;
using Boar.Red.Service.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Services;

[Service]
public class GatewayRestartService(IHostApplicationLifetime applicationLifetime)
{
	////////////////////////////////////////////////
	// @静态常量
	////////////////////////////////////////////////

	#region 静态常量

	public const string RestartParentProcessArgument = "--gateway-restart-parent-process";

	#endregion

	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	private int _isRestartScheduled;

	#endregion

	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public static ProcessStartInfo CreateRestartStartInfo(string processPath, string[] commandLineArgs, int parentProcessId, string workingDirectory)
	{
		var startInfo = new ProcessStartInfo(processPath)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			WorkingDirectory = workingDirectory
		};
		var isDotNetHost = string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase);
		for (var index = isDotNetHost ? 0 : 1; index < commandLineArgs.Length; index++)
		{
			if (commandLineArgs[index] == RestartParentProcessArgument)
			{
				index++;
				continue;
			}
			startInfo.ArgumentList.Add(commandLineArgs[index]);
		}
		startInfo.ArgumentList.Add(RestartParentProcessArgument);
		startInfo.ArgumentList.Add(parentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
		return startInfo;
	}

	public static async Task<string[]> WaitForParentExitAsync(string[] args)
	{
		var remainingArgs = new List<string>();
		for (var index = 0; index < args.Length; index++)
		{
			if (args[index] != RestartParentProcessArgument)
			{
				remainingArgs.Add(args[index]);
				continue;
			}
			if (++index >= args.Length || !int.TryParse(args[index], out var parentId) || parentId <= 0 || parentId == Environment.ProcessId)
			{
				throw new ArgumentException("网关重启的父进程参数无效。");
			}
			Process parent;
			try
			{
				parent = Process.GetProcessById(parentId);
			}
			catch (ArgumentException)
			{
				continue;
			}
			using (parent)
			using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
			{
				await parent.WaitForExitAsync(timeout.Token);
			}
		}
		return remainingArgs.ToArray();
	}

	public void ScheduleRestart(HttpResponse response)
	{
		var processPath = Environment.ProcessPath;
		var processName = Path.GetFileNameWithoutExtension(processPath);
		if (string.IsNullOrEmpty(processPath) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_POOL_ID"))
			|| string.Equals(processName, "w3wp", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(processName, "iisexpress", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("当前宿主不支持进程内重启，请通过 IIS 应用程序池重启网关。");
		}
		if (Interlocked.CompareExchange(ref _isRestartScheduled, 1, 0) != 0)
		{
			throw new InvalidOperationException("网关已在等待重启。");
		}
		try
		{
			// 子进程先等待本进程退出，响应完成后才停止当前网关，避免端口争用。
			var startInfo = CreateRestartStartInfo(processPath, Environment.GetCommandLineArgs(), Environment.ProcessId, Environment.CurrentDirectory);
			using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动新的网关进程。");
			response.OnCompleted(() =>
			{
				Log.UserOperate.Logs(this, "配置接口已返回，开始重启网关程序。");
				applicationLifetime.StopApplication();
				return Task.CompletedTask;
			});
		}
		catch
		{
			Interlocked.Exchange(ref _isRestartScheduled, 0);
			throw;
		}
	}

	#endregion
}
