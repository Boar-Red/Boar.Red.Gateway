namespace Boar.Red.Gateway.Constants
{
	public enum ResponseCacheMode
	{
		/// <summary>
		/// 未知缓存模式。
		/// </summary>
		Unknown = 0,

		/// <summary>
		/// 不缓存。
		/// </summary>
		NoneCache = 1,

		/// <summary>
		/// 返回缓存，并自动定时更新缓存。
		/// </summary>
		ReturnCache_AutoUpdateCache = 2,

		/// <summary>
		/// 返回缓存，并自动定时更新缓存。
		/// </summary>
		ReturnEndPointResponse_AndSaveToCache = 3,

		/// <summary>
		/// 返回缓存，并由可信任者主动更新缓存。
		/// </summary>
		ReturnCache_UpdateCacheManual = 4
	}
}
