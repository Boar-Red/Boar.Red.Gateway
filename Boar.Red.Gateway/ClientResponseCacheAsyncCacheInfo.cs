using Boar.Red.Gateway.Models;

namespace Boar.Red.Gateway
{
	public class ClientResponseCacheAsyncCacheInfo
	{

		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性

		public ClientResponseCache ClientResponseCache { get; set; }

		public ClientResponseCacheCreateParam? ClientResponseCacheCreateParam { get; set; }

		#endregion



		////////////////////////////////////////////////
		// @自身实现
		////////////////////////////////////////////////

		#region 自身实现

		public ClientResponseCacheAsyncCacheInfo(
		    ClientResponseCache clientResponseCache,
		    ClientResponseCacheCreateParam? clientResponseCacheCreateParam)
		{
			this.ClientResponseCache = clientResponseCache;
			this.ClientResponseCacheCreateParam = clientResponseCacheCreateParam;
		}

		#endregion

	}
}
