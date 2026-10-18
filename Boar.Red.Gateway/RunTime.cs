using Boar.Red.Gateway.Utils;
using System;
using System.Threading;

namespace Boar.Red.Gateway
{
	public class RunTime
	{

		////////////////////////////////////////////////
		// @静态常量
		////////////////////////////////////////////////

		#region 静态常量

		private static RouteRequestPathOptions _routeRequestPaths = RouteRequestPathOptions.Default;

		protected static bool _isDefaultOptionRecorded = false;

		protected static int _systemWorkerThreadsCountMinDefault;

		protected static int _systemWorkerThreadsCountMaxDefault;

		protected static int _systemIOThreadsMinDefault;

		protected static int _systemIOThreadsMaxDefault;

		#endregion

		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性

		/// <summary>仅在启动注册入口时设置；配置热更新不会改变本次进程的 URL 语法。</summary>
		public static RouteRequestPathOptions RouteRequestPaths => Volatile.Read(ref _routeRequestPaths);

		#endregion

		////////////////////////////////////////////////
		// @类方法
		////////////////////////////////////////////////

		#region 类方法

		public static void InitializeRouteRequestPaths(RouteRequestPathOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);
			if (Interlocked.CompareExchange(ref _routeRequestPaths, options, RouteRequestPathOptions.Default) != RouteRequestPathOptions.Default)
			{
				throw new InvalidOperationException("路由 URL 前缀已初始化，修改前缀需要重启网关程序。");
			}
		}

		public static void UpateOptions(ConfigFiles.RouterConfig routerConfig)
		{
			if (!_isDefaultOptionRecorded)
			{
				// !!!
				_isDefaultOptionRecorded = true;
				// !!!

				ThreadPool.GetMinThreads(
				    out _systemWorkerThreadsCountMinDefault,
				    out _systemIOThreadsMinDefault);

				ThreadPool.GetMaxThreads(
				    out _systemWorkerThreadsCountMaxDefault,
				    out _systemIOThreadsMaxDefault);
			}

			////////////////////////////////////////////////

			var workerThreadsCountMin = routerConfig.WorkerThreadsCountMin;
			if (workerThreadsCountMin <= 0)
			{
				workerThreadsCountMin = _systemWorkerThreadsCountMinDefault;
			}
			var ioThreadsMin = routerConfig.IOThreadsMin;
			if (ioThreadsMin <= 0)
			{
				ioThreadsMin = _systemIOThreadsMinDefault;
			}
			ThreadPool.SetMinThreads(
			    workerThreadsCountMin,
			    ioThreadsMin);

			////////////////////////////////////////////////

			var workerThreadsCountMax = routerConfig.WorkerThreadsCountMax;
			if (workerThreadsCountMax <= 0)
			{
				workerThreadsCountMax = _systemWorkerThreadsCountMaxDefault;
			}
			var ioThreadsMax = routerConfig.IOThreadsMax;
			if (ioThreadsMax <= 0)
			{
				ioThreadsMax = _systemIOThreadsMaxDefault;
			}
			ThreadPool.SetMaxThreads(
			    workerThreadsCountMax,
			    ioThreadsMax);
		}

		#endregion
	}
}
