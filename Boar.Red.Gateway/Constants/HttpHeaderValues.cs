namespace Boar.Red.Gateway.Constants
{
	public static class HttpHeaderValues
	{
		public static class Content_Types
		{
			public const string Text_Plain_Utf8 = "text/plain;charset=UTF-8";

			public const string Application_Json_Utf8 = "application/json;charset=UTF-8";
		}

		public static class Content_Encodings
		{
			/// <summary>
			/// 压缩方法，Br（Brotli compressed data format）。
			/// </summary>
			public const string Br = "br";

			/// <summary>
			/// 压缩方法，GZip。
			/// </summary>
			public const string GZip = "gzip";

			/// <summary>
			/// 压缩方法，Deflate。
			/// </summary>
			public const string Deflate = "deflate";

			/// <summary>
			/// 不压缩。
			/// </summary>
			public const string Identity = "identity";
		}

		public static class RouteResponseTypes
		{
			public const string OriginalData = "OriginalData";

			public const string CacheDataFromStorage = "CacheDataFromStorage";

			public const string CacheData = "CacheData";
		}
	}
}
