using BaoXia.Utils;

namespace Boar.Red.Gateway.LogFiles
{
	public class Log
	{
		/// <summary>
		/// 日常普通日志。
		/// </summary>
		public static readonly LogFile Logs = new("", "Log");

		/// <summary>
		/// Http请求日志。
		/// </summary>
		public static readonly LogFile HttpRequest = new("Http请求日志", "HttpRequest");

		/// <summary>
		/// 用户操作日志。
		/// </summary>
		public static readonly LogFile UserOperate = new("用户操作日志", "UserOperate");

		/// <summary>
		/// 开发调试日志。
		/// </summary>
		public static readonly LogFile Debug = new("调试日志", "Debug");

		/// <summary>
		/// SQL语句日志。
		/// </summary>
		public static readonly LogFile SQL = new("SQL", "SQL");

		/// <summary>
		/// 性能日志。
		/// </summary>
		public static readonly LogFile Performance = new("性能日志", "Performance");

		/// <summary>
		/// 峰值日志。
		/// </summary>
		public static readonly LogFile RPSPeak = new("峰值日志", "RPSPeak");

		/// <summary>
		/// 程序异常日志。
		/// </summary>
		public static readonly LogFile Exception = new("异常日志", "Exception");

		/// <summary>
		/// 运营警告日志。
		/// </summary>
		public static readonly LogFile Warning = new("⚠警告日志", "Warning");

		/// <summary>
		/// Boar.Red服务日志。
		/// </summary>
		public static readonly LogFile BrService = new("Boar.Red 服务日志", "BrService");

		/// <summary>
		/// 数据集保存器日志。
		/// </summary>
		public static readonly LogFile DbSetSaver = new("数据集保存器日志", "DbSetSaver");
	}
}
