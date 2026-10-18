using Boar.Red.Gateway.Constants;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;

namespace Boar.Red.Gateway.Models
{
	public class ClientResponseInfo
	{

		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性

		public ResponseExceptionProcess EndPointResponseExceptionProcess { get; set; }

		public ResponseCacheMode CacheMode { get; set; } = ResponseCacheMode.Unknown;

		public double CacheUpdateIntervalSeconds { get; set; }

		public bool IsNeedReturnCacheFirst
		{
			get
			{
				if (this.CacheMode == ResponseCacheMode.ReturnCache_AutoUpdateCache
				    || this.CacheMode == ResponseCacheMode.ReturnCache_UpdateCacheManual)
				{
					return true;
				}
				return false;
			}
		}

		public bool IsNeedSaveResponseCache
		{
			get
			{
				if (this.CacheMode == ResponseCacheMode.ReturnCache_AutoUpdateCache
				    || this.CacheMode == ResponseCacheMode.ReturnEndPointResponse_AndSaveToCache
				    || this.CacheMode == ResponseCacheMode.ReturnCache_UpdateCacheManual)
				{
					return true;
				}
				return false;
			}
		}

		public DateTime ExpireTime { get; set; }

		public int ResponseHttpStatusCode { get; set; }

		public Dictionary<string, StringValues>? ResponseHttpHeader { get; set; }

		public byte[]? ResponseBodyBytes { get; set; }

		public bool IsValid
		{
			get
			{
				if (this.ExpireTime > DateTime.Now
				    && this.ResponseHttpHeader?.Count > 0
				    && this.ResponseBodyBytes?.Length > 0)
				{
					return true;
				}
				return false;
			}
		}

		#endregion



		////////////////////////////////////////////////
		// @自身实现
		////////////////////////////////////////////////

		#region 自身实现

		public ClientResponseInfo(
		    ResponseExceptionProcess endPointResponseExceptionProcess,
		    //
		    ResponseCacheMode cacheMode,
		    double cacheUpdateIntervalSeconds,
		    DateTime expireTime,
		    //
		    int responseHttpStatusCode,
		    Dictionary<string, StringValues>? responseHttpHeader,
		    byte[]? responseBodyBytes)
		{
			this.EndPointResponseExceptionProcess = endPointResponseExceptionProcess;

			this.CacheMode = cacheMode;
			this.CacheUpdateIntervalSeconds = cacheUpdateIntervalSeconds;

			this.ExpireTime = expireTime;

			this.ResponseHttpStatusCode = responseHttpStatusCode;
			this.ResponseHttpHeader = responseHttpHeader;
			this.ResponseBodyBytes = responseBodyBytes;
		}

		#endregion
	}
}
