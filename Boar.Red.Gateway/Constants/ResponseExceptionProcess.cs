namespace Boar.Red.Gateway.Constants
{
	public enum ResponseExceptionProcess
	{
		/// <summary>
		/// 返回异常信息。
		/// </summary>
		ReturnExceptionInfo = 0,

		/// <summary>
		/// 返回上一次正确的响应缓存。
		/// </summary>
		ReturnLastResponseCache = 1
	}
}
