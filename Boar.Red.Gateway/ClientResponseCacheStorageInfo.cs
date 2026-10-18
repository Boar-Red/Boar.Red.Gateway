namespace Boar.Red.Gateway
{
	public class ClientResponseCacheStorageInfo
	{

		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性

		public int CountCreated { get; set; }

		public int CountUpdated { get; set; }

		#endregion


		////////////////////////////////////////////////
		// @自身实现
		////////////////////////////////////////////////

		#region 自身实现

		ClientResponseCacheStorageInfo()
		{ }

		ClientResponseCacheStorageInfo(
		    int countCreated,
		    int countUpdated)
		{
			this.CountCreated = countCreated;
			this.CountUpdated = countUpdated;
		}

		#endregion

	}
}
