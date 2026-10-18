using BaoXia.Utils;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.LogFiles;
using Boar.Red.Service.Models;
using Boar.Red.Service.WebService.Models;
using System;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.ConfigFiles;

public class RouterConfig : ConfigFile
{
	////////////////////////////////////////////////
	// @静态常量
	////////////////////////////////////////////////

	#region 静态常量

	/// <summary>
	/// 日志文件夹路径，默认为："LogFiles/"。
	/// </summary>
	public const string LogFilesDirectoryPathDefault = "LogFiles/";

	/// <summary>
	/// Http请求内容格式化器默认值（全部值）：。
	/// </summary>
	public const string HttpRequestLogContentFormatterDefault = "RequestTime,RequestUrl,RequestBodySize,ClientIP,ClientPort,ClientUserAgent,ResponseTime,ResponseSize,ResponseDurationSeconds,ResponseSendSeconds,RouterDurationSeconds,EndPointDurationSeconds";

	public static readonly string[] HttpRequestLogContentFormatterKeys = HttpRequestLogContentFormatterDefault.Split(
		',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	/// <summary>
	/// SMTP服务器发送邮件的间隔秒数默认值：60 秒。
	/// </summary>
	public const double SMTPServerSendMailIntervalSecondsDefault = 60.0;

	/// <summary>
	/// 请求最大内容长度默认值：16 MB。
	/// </summary>
	public const int ClientContentLengthMaxDefault = (1024 * 1024) * 16;

	/// <summary>
	/// 响应时长排行榜长度默认值：1000 个。
	/// </summary>
	public const int ResponseDurationRankListLengthDefault = 1000;

	/// <summary>
	/// 响应时长超出最大值时，向管理员发出消息的标题默认值。
	/// </summary>
	public const string ResponseDurationMaxToWarningManagerMessageTitleDefault = "一些接口的响应时长超出了最大值";

	/// <summary>
	/// 响应时长超出最大值时，向管理员发出消息的内容格式器默认值，最大时长的关键字为：#最大响应时间#，超时的链接关键字为：#超时请求Urls#。
	/// </summary>
	public const string ResponseDurationMaxToWarningManagerMessageFormatterDefault = "以下请求的响应时间超过了“#最大响应时间#”：\r\n#超时请求Urls#\r\n消息结束。";

	/// <summary>
	/// 路由请求的，最长超时秒数默认值：10 秒。
	/// </summary>
	public const double EndPointRequestTimeoutSecondsMaxDefault = 10;

	/// <summary>
	/// 路由响应缓存的，有效Http状态码默认值：[ 200 ]。
	/// </summary>
	public static readonly int[] EndPointResponseCacheValidHttpStatusCodesDefault = [200];

	/// <summary>
	/// 路由缓存，异步缓存间隔秒数最小值的默认值：1 秒。
	/// </summary>
	public const double ClientResponseCacheAsyncCacheIntervalSecondsDefault = 1;

	/// <summary>
	/// 路由缓存，自动更新间隔秒数最小值的默认值：1 秒。
	/// </summary>
	public const double ClientResponseCacheAutoUpdateIntervalSecondsDefault = 1;

	/// <summary>
	/// 路由缓存，自动更新使用的线程数默认值：20 个。
	/// </summary>
	public const int ClientResponseCacheAutoUpdateThreadsCountDefault = 20;

	/// <summary>
	/// 路由缓存，自动释放缓存的，检查间隔秒数默认值：1800 秒。
	/// </summary>
	public const double ClientResponseCacheIntervalSecondsToCleanDefault = 1.0;

	/// <summary>
	/// 路由缓存，自动释放缓存的，未访问秒数默认值：1800 秒。
	/// </summary>
	public const double ClientResponseCacheNoneReadSecondsToReleaseDefault = 60 * 30;

	/// <summary>
	/// 路由缓存，自动持久化间隔秒数的默认值：1 秒。
	/// </summary>
	public const double ClientResponseCacheAutoStorageIntervalSecondsDefault = 60;

	/// <summary>
	/// 路由缓存，自动存储时每次存储的缓存数量默认值。
	/// </summary>
	public const int ClientResponseCachesCountToAutoStorageDefault = 1000;

	/// <summary>
	/// 路由缓存，自动清除持久化存储的，未访问秒数默认值：604800 秒，即 7 天。
	/// </summary>
	public const double ClientResponseCacheNoneReadSecondsToReleaseStorageDefault = 60 * 60 * 24 * 7;

	/// <summary>
	/// 路由缓存，自动清除持久化存储时，每次清除的个数默认值：1000 个。
	/// </summary>
	public const int ClientResponseCachesCountToReleaseStorageDefault = 1000;

	#endregion


	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	/// <summary>
	/// 长连接服务配置。
	/// </summary>
	public Boar.Red.Service.BrService.Models.ServiceConfig? BrServiceConfig { get; set; }

	/// <summary>
	/// Web服务基础配置。
	/// </summary>
	public WebServiceConfig? WebServiceConfig { get; set; }

	/// <summary>
	/// 服务管理员配置。
	/// </summary>
	public ServiceAdministratorConfig? ServiceAdministratorConfig { get; set; }

	/// <summary>管理员在管理页面中的标识与显示名称。</summary>
	public Boar.Red.Gateway.Models.AdministratorMetadataInfo[]? AdministratorMetadataInfes { get; set; }

	[System.Text.Json.Serialization.JsonIgnore]
	public object SyncRoot { get; } = new();


	#endregion

	#region  基础信息设置，日志，统计参数等。

	/// <summary>
	/// 日志文件夹路径，默认为："LogFiles/"。
	/// </summary>
	public string LogFilesDirectoryPath { get; set; } = LogFilesDirectoryPathDefault;

	/// <summary>
	/// 日志文件自动存储的时间间隔。
	/// </summary>
	public double AutoFlushLogBufferIntervalSeconds { get; set; }

	/// <summary>
	/// 每个日志文件的最大长度，单位为：字节。
	/// </summary>
	public int MaxBytesCountPerLogFile { get; set; }

	/// <summary>
	/// 每次日志文件写入的最多日志条目数。
	/// </summary>
	public int LogRecordsCountPerFileWrite { get; set; }

	/// <summary>
	/// 日志文件写入的最长超时，单位为：秒。
	/// </summary>
	public double TimeoutSecondsToStorageLogRecords { get; set; }

	/// <summary>
	/// 异常日志需要忽略的关键字。
	/// </summary>
	public string[]? ExceptionLogIgnoreKeys { get; set; }

	/// <summary>
	/// 是否开启Http请求日志。
	/// </summary>
	public bool IsHttpRequestLogEnable { get; set; } = false;

	/// <summary>
	/// Http请求内容格式化器。
	/// </summary>
	public string HttpRequestLogContentFormatter { get; set; } = HttpRequestLogContentFormatterDefault;

	/// <summary>
	/// SMTP服务器地址。
	/// </summary>
	public string? SMTPServerAddress { get; set; }

	/// <summary>
	/// SMTP服务器端口。
	/// </summary>
	public int SMTPServerPort { get; set; }

	/// <summary>
	/// SMTP服务器是否开启了SSL。
	/// </summary>
	public bool IsSMTPServerSSLEnable { get; set; }

	/// <summary>
	/// SMTP服务器账号。
	/// </summary>
	public string? SMTPServerAccount { get; set; }

	/// <summary>
	/// SMTP服务器密码。
	/// </summary>
	public string? SMTPServerPassword { get; set; }

	/// <summary>
	/// SMTP服务器昵称
	/// </summary>
	public string? SMTPAuthorName { get; set; }

	/// <summary>
	/// SMTP服务器发送邮件的间隔秒数，。
	/// </summary>
	public double SMTPServerSendMailIntervalSeconds { get; set; } = SMTPServerSendMailIntervalSecondsDefault;

	#endregion

	#region 路由性能设置。

	/// <summary>
	/// 工作线程数量，最小值，“0”为系统默认。
	/// </summary>
	public int WorkerThreadsCountMin { get; set; } = 0;

	/// <summary>
	/// 工作线程数量，最大值，“0”为系统默认。
	/// </summary>
	public int WorkerThreadsCountMax { get; set; } = 0;

	/// <summary>
	/// IO线程数量，最小值，“0”为系统默认。
	/// </summary>
	public int IOThreadsMin { get; set; } = 0;

	/// <summary>
	/// IO线程数量，最大值，“0”为系统默认。
	/// </summary>
	public int IOThreadsMax { get; set; } = 0;

	#endregion;

	#region 路由中转参数，请求哈希值，有效转发值，压缩方法等。

	/// <summary>
	/// 计算请求哈希值的，HTTP头部关键字数组。
	/// </summary>
	public string[]? ClientRequestHttpHeaderHashKeys { get; set; } = ["Method", "Content-Type"];

	/// <summary>
	/// 转发请求的，HTTP头部关键字数组。
	/// </summary>
	public string[]? ClientRequestHttpHeaderValidKeys { get; set; } = ["Method", "Cookie", "Content-Type", "Last-Event-ID"];

	/// <summary>
	/// 是否转发客户端Cookie值时，使用转义：会过滤掉部分不合法的Cookie值。
	/// </summary>
	public bool IsClientCookieRouteWithoutEscape { get; set; }

	/// <summary>
	/// 每秒最大请求数，0为不限制。
	/// </summary>
	public double EndPointRPSMax { get; set; }

	/// <summary>
	/// 是否唯一的终点强制可用。
	/// </summary>
	public bool IsSoleEndPointForcedEnable { get; set; }

	/// <summary>
	/// 转发响应的，HTTP头部关键字数组。
	/// </summary>
	public string[]? ClientResponseHttpHeaderValidKeys { get; set; } = ["Content-Type", "Set-Cookie"];

	/// <summary>
	/// 响应压缩算法，可选值为：
	/// none，不压缩；
	/// auto，默认压缩算法，自动识别请求，提供针对性的压缩；
	/// br，强制使用“Br”（Brotli compressed data format）压缩算法；
	/// gzip，强制使用“GZip”压缩算法；
	/// deflate，强制使用“Deflate”压缩算法。
	/// </summary>
	public string? ClientResponseCompressionMethod { get; set; } = ResponseCompressionMethods.Auto;

	/// <summary>
	/// 响应压缩支持的内容类型数组，如：
	/// application/json，text/plain，text/html。
	/// </summary>
	public string[]? ClientResponseCompressionContentTypes { get; set; }

	/// <summary>
	/// 响应压缩支持的内容的最小长度（比特），如：1048576（1MB）。
	/// </summary>
	public int ClientResponseCompressionContentMinLength { get; set; } = 1048576;


	/// <summary>
	/// 路由统计信息保留时长，单位为：秒，默认为： 3600 秒（1小时）。
	/// </summary>
	public double RouteStatisticsInfoRetainSeconds { get; set; } = 3600.0;

	/// <summary>
	/// 响应时长排行榜长度，默认为：1000 个，当设置为0时，则按默认值处理。
	/// </summary>
	public int ResponseDurationRankListLength = ResponseDurationRankListLengthDefault;

	/// <summary>
	/// 响应时长排行榜，要排除的Url关键字数组。
	/// </summary>
	public string[]? ResponseDurationRankListExceptedUrlKeys { get; set; }

	/// <summary>
	/// 响应时长最大值，超出该值时，会通知管理者，设置为“0”时，永远不会触发通知。
	/// </summary>
	public double ResponseDurationMillisecondsMaxToWarningManager { get; set; }

	/// <summary>
	/// 响应时长超出最大值时，向管理员发出消息的标题。
	/// </summary>
	public string? ResponseDurationMaxToWarningManagerMessageTitle { get; set; } = ResponseDurationMaxToWarningManagerMessageTitleDefault;

	/// <summary>
	/// 响应时长超出最大值时，向管理员发出消息的内容格式器，最大时长的关键字为：#最大响应时间#，超时的链接关键字为：#超时请求Urls#。
	/// </summary>
	public string? ResponseDurationMaxToWarningManagerMessageFormatter { get; set; } = ResponseDurationMaxToWarningManagerMessageFormatterDefault;


	/// <summary>
	/// 路由统计信息峰值定义，单位为：RPS，默认为：0，即未定义。
	/// </summary>
	public int RouteStatisticsInfoRPSPeakDefinition { get; set; }

	#endregion


	////////////////////////////////////////////////
	////////////////////////////////////////////////
	////////////////////////////////////////////////


	#region 路由缓存相关设置。

	/// <summary>
	/// 路由请求的，最长超时秒数。
	/// </summary>
	public double EndPointRequestTimeoutSecondsMax { get; set; } = EndPointRequestTimeoutSecondsMaxDefault;

	/// <summary>
	/// 终点异常比例观察时长，单位为：秒，0 为不观察，-1 为采用默认设置。
	/// </summary>
	public double EndPointExceptionRateWatchingSeconds { get; set; }

	/// <summary>
	/// 终点异常比例观察数量最小值，0 为不限制，-1 为采用默认设置。
	/// </summary>
	public double EndPointExceptionRateWatchingRequestsCountMin { get; set; }

	/// <summary>
	/// 终点最大异常比例，0 为不限制，-1 为采用默认设置。
	/// </summary>
	public double EndPointExceptionRateMax { get; set; }

	/// <summary>
	/// 终点异常后，重试的间隔秒数，0 为不重试，-1 为采用默认设置。
	/// </summary>
	public double EndPointExceptionRetryIntervalSeconds { get; set; }

	/// <summary>
	/// 路由响应缓存的，有效Http状态码。
	/// </summary>
	public int[]? EndPointResponseCacheValidHttpStatusCodes { get; set; } = EndPointResponseCacheValidHttpStatusCodesDefault;

	/// <summary>
	/// 路由缓存，异步缓存间隔秒数的最小值。
	/// </summary>
	public double ClientResponseCacheAsyncCacheIntervalSeconds { get; set; } = ClientResponseCacheAsyncCacheIntervalSecondsDefault;

	/// <summary>
	/// 路由缓存，自动更新间隔秒数的最小值。
	/// </summary>
	public double ClientResponseCacheAutoUpdateIntervalSeconds { get; set; } = ClientResponseCacheAutoUpdateIntervalSecondsDefault;

	/// <summary>
	/// 路由缓存，自动更新使用的线程数。
	/// </summary>
	public int ClientResponseCacheAutoUpdateThreadsCount { get; set; } = ClientResponseCacheAutoUpdateThreadsCountDefault;

	protected double _clientResponseCacheIntervalSecondsToClean = ClientResponseCacheIntervalSecondsToCleanDefault;

	/// <summary>
	/// 路由缓存，自动释放缓存的，检查间隔秒数。
	/// </summary>
	public double ClientResponseCacheIntervalSecondsToClean
	{
		get
		{
			var clientResponseCacheIntervalSecondsToClean
			    = _clientResponseCacheIntervalSecondsToClean;
			if (clientResponseCacheIntervalSecondsToClean <= 0.0)
			{
				clientResponseCacheIntervalSecondsToClean = ClientResponseCacheIntervalSecondsToCleanDefault;

			}
			return clientResponseCacheIntervalSecondsToClean;

		}
		set
		{
			_clientResponseCacheIntervalSecondsToClean = value;

		}
	}

	/// <summary>
	/// 路由缓存，自动释放缓存的，未访问秒数。
	/// </summary>
	public double ClientResponseCacheNoneReadSecondsToRelease { get; set; } = ClientResponseCacheNoneReadSecondsToReleaseDefault;

	/// <summary>
	/// 路由缓存，自动持久化间隔秒数的。
	/// </summary>
	public double ClientResponseCacheAutoStorageIntervalSeconds { get; set; } = ClientResponseCacheAutoStorageIntervalSecondsDefault;

	/// <summary>
	/// 路由缓存，自动存储时每次存储的缓存数量。
	/// </summary>
	public int ClientResponseCachesCountToAutoStorage { get; set; } = ClientResponseCachesCountToAutoStorageDefault;

	/// <summary>
	/// 路由缓存，自动清除持久化存储的，未访问秒数。
	/// </summary>
	public double ClientResponseCacheNoneReadSecondsToReleaseStorage { get; set; } = ClientResponseCacheNoneReadSecondsToReleaseStorageDefault;

	/// <summary>
	/// 路由缓存，自动清除持久化存储时，每次清除的个数。
	/// </summary>
	public int ClientResponseCachesCountToReleaseStorage { get; set; } = ClientResponseCachesCountToReleaseStorageDefault;

	#endregion


	////////////////////////////////////////////////
	////////////////////////////////////////////////
	////////////////////////////////////////////////


	#region 路由规则。

	/// <summary>请求 URL 中的路由目录前缀；默认为 @，空字符串表示无前缀，重启后生效。</summary>
	public string RouteRequestPathSpliter { get; set; } = PathKeywords.RouteRequestPathSpliter;

	/// <summary>
	/// 路由规则。
	/// </summary>
	public RouteRule[]? RouteRules { get; set; }

	#endregion


	////////////////////////////////////////////////
	// @重载
	////////////////////////////////////////////////

	#region 重载

	protected override void DidConfigFileChanged(object sender, System.IO.FileSystemEventArgs fileSystemEventArgs)
	{
		lock (SyncRoot)
		{
			base.DidConfigFileChanged(sender, fileSystemEventArgs);
		}
	}

	protected override async Task DidLoadConfigFileCompletedFromFilePathAsync(string filePath, ConfigFile configFile)
	{
		await base.DidLoadConfigFileCompletedFromFilePathAsync(filePath, configFile);

		await Task.Run(() =>
		{
			// !!!⚠ 同步设置日志文件相关属性 ⚠!!!
			{
				var logFilesDirectoryPath = this.LogFilesDirectoryPath;
				if (logFilesDirectoryPath == null || logFilesDirectoryPath.Length < 1)
				{
					logFilesDirectoryPath = RouterConfig.LogFilesDirectoryPathDefault;
				}
				BaoXia.Utils.Environment.LogFilesDirectoryPath = logFilesDirectoryPath;

				Log.Exception.KeysInLogContentToIgnoreLogs = this.ExceptionLogIgnoreKeys;
			}



			////////////////////////////////////////////////
			// !!!⚠ 异步更新运行时 ⚠!!!
			// !!!⚠ 避免静态的配置对象，与使用配置对象的静态对象，嵌套调用 ⚠!!!
			RunTime.UpateOptions(this);
			// !!!
			////////////////////////////////////////////////

			////////////////////////////////////////////////
			// !!!⚠ 异步更新路由器 ⚠!!!
			// !!!⚠ 避免静态的配置对象，与使用配置对象的静态对象，嵌套调用 ⚠!!!
			Router.UpateRouters(this);
			// !!!
			////////////////////////////////////////////////


			////////////////////////////////////////////////
			// !!!⚠ 异步更新路由缓存 ⚠!!!
			// !!!⚠ 避免静态的配置对象，与使用配置对象的静态对象，嵌套调用 ⚠!!!
			ClientResponseCaches.UpateCaches(this);
			// !!!
			////////////////////////////////////////////////
		});
	}

	#endregion
}
