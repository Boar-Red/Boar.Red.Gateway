using BaoXia.Utils.Extensions;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Microsoft.Extensions.Primitives;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Boar.Red.Gateway.Models
{
	public class EndPointRequestInfo : RequestInfo
	{
		////////////////////////////////////////////////
		// @静态变量
		////////////////////////////////////////////////

		#region 静态变量

		public static readonly ArrayPool<byte> _bytesPool = ArrayPool<byte>.Shared;

		#endregion



		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性

		/// <summary>
		/// 请求的终点对象。
		/// </summary>
		[NotMapped, JsonIgnore]
		[MemberNotNullWhen(true, "IsValidToRequestToEndPoint")]
		public EndPoint? EndPoint { get; set; }

		/// <summary>
		/// 请求的终点名称。
		/// </summary>
		public string? EndPointName { get; set; }

		/// <summary>
		/// 请求Url。
		/// </summary>
		[MemberNotNullWhen(true, "IsValid")]
		public string? EndPointRequestUrl { get; set; }

		/// <summary>
		/// 用于计算终点请求哈希值的Http头部参数关键字数组。
		/// </summary>
		[NotMapped, JsonIgnore]
		public string[]? EndPointRequestHttpHeaderHashKeys { get; set; }

		protected string? _endPointRequestPersistedHashCode = null;
		protected int _endPointRequestRunningHashCode = 0;

		/// <summary>
		/// 终点请求的【持久化哈希码】，字符串类型，持久有效。
		/// </summary>
		public string? EndPointRequestPersistedHashCode
		{
			get
			{
				return _endPointRequestPersistedHashCode;
			}
			set
			{
				_endPointRequestPersistedHashCode = value;
				_endPointRequestRunningHashCode
				    = _endPointRequestPersistedHashCode?.GetHashCode()
				    ?? 0;
			}
		}

		/// <summary>
		/// 终点请求的【内存哈希码】，整数类型，只在程序运行期间有效。
		/// </summary>
		public int EndPointRequestRunningHashCode => _endPointRequestRunningHashCode;

		/// <summary>
		/// 终点请求的超时秒数。
		/// </summary>
		public double EndPointRequestTimeoutSeconds { get; set; }

		/// <summary>
		/// 终点响应转发模式。
		/// </summary>
		public EndPointResponseForwardMode EndPointResponseForwardMode { get; set; }

		/// <summary>
		/// 流式转发时每次读取并发送的字节数。
		/// </summary>
		public int EndPointResponseForwardBytesCount { get; set; }

		/// <summary>
		/// 响应异常的处理类型。
		/// </summary>
		public ResponseExceptionProcess EndPointResponseExceptionProcess { get; set; } = ResponseExceptionProcess.ReturnExceptionInfo;

		/// <summary>
		/// 响应内容的缓存模式。
		/// </summary>
		public ResponseCacheMode ClientResponseCacheMode { get; set; } = ResponseCacheMode.NoneCache;

		/// <summary>
		/// 响应内容的缓存秒数。
		/// </summary>
		public double ClientResponseCacheUpdateIntervalSeconds { get; set; }


		/// <summary>
		/// 当前响应缓存是否合法有效。 @will
		/// </summary>
		public bool IsValid
		{
			get
			{
				if (this.EndPointRequestUrl?.Length > 0)
				{
					return true;
				}
				return false;
			}
		}

		/// <summary>
		/// 当前响应缓存是否合法有效，并且可请求至终点。
		/// </summary>
		public bool IsValidToRequestToEndPoint
		{
			get
			{
				if (this.IsValid
				    && this.EndPoint != null)
				{
					return true;
				}
				return false;
			}
		}

		/// <summary>
		/// 当前请求是否需要优先返回响应缓存。
		/// </summary>
		public bool IsNeedReturnCacheFirst
		{
			get
			{
				if (this.ClientResponseCacheMode == ResponseCacheMode.ReturnCache_AutoUpdateCache
				    || this.ClientResponseCacheMode == ResponseCacheMode.ReturnCache_UpdateCacheManual)
				{
					return true;
				}
				return false;
			}
		}


		/// <summary>
		/// 当前请求是否需要保存响应缓存。
		/// </summary>
		public bool IsNeedSaveResponseCache
		{
			get
			{
				if (this.ClientResponseCacheMode == ResponseCacheMode.ReturnCache_AutoUpdateCache
				    || this.ClientResponseCacheMode == ResponseCacheMode.ReturnEndPointResponse_AndSaveToCache
				    || this.ClientResponseCacheMode == ResponseCacheMode.ReturnCache_UpdateCacheManual)
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

		public EndPointRequestInfo()
		{ }

		public EndPointRequestInfo(
		    RequestInfo clientRequestInfo,
		    string[]? endPointRequestHttpHeaderHashKeys,
		    //
		    EndPoint? endPoint,
		    string? endpointName,
		    string? endPointRequestUrl,
		    double endPointRequestTimeoutSeconds,
		    EndPointResponseForwardMode endPointResponseForwardMode,
		    int endPointResponseForwardBytesCount,
		    ResponseExceptionProcess endPointResponseExceptionProcess,
		    //
		    ResponseCacheMode cacheMode,
		    double cacheUpdateIntervalSeconds)
		    : base(clientRequestInfo)
		{
			this.EndPoint = endPoint;
			this.EndPointName = endpointName;
			this.EndPointRequestUrl = endPointRequestUrl;

			this.EndPointRequestHttpHeaderHashKeys = endPointRequestHttpHeaderHashKeys;

			this.EndPointRequestTimeoutSeconds = endPointRequestTimeoutSeconds;
			this.EndPointResponseForwardMode = endPointResponseForwardMode;
			this.EndPointResponseForwardBytesCount = endPointResponseForwardBytesCount;
			this.EndPointResponseExceptionProcess = endPointResponseExceptionProcess;


			this.ClientResponseCacheMode = cacheMode;
			this.ClientResponseCacheUpdateIntervalSeconds = cacheUpdateIntervalSeconds;
		}

		public EndPointRequestInfo(Models.ClientResponseCache clientResponseCache)
		    : base(clientResponseCache.RequestPath,
		      clientResponseCache.RequestQueryString,
		      clientResponseCache.RequestHttpHeader,
		      clientResponseCache.RequestBodyBytes)
		{
			EndPoint = clientResponseCache.EndPoint;
			EndPointName = clientResponseCache.EndPointName;
			EndPointRequestUrl = clientResponseCache.EndPointRequestUrl;
			EndPointRequestTimeoutSeconds = clientResponseCache.EndPointRequestTimeoutSeconds;
			EndPointResponseForwardMode = clientResponseCache.EndPointResponseForwardMode;
			EndPointResponseForwardBytesCount = clientResponseCache.EndPointResponseForwardBytesCount;
			EndPointResponseExceptionProcess = clientResponseCache.EndPointResponseExceptionProcess;

			// !!!
			_endPointRequestPersistedHashCode = clientResponseCache._endPointRequestPersistedHashCode;
			_endPointRequestRunningHashCode = clientResponseCache._endPointRequestRunningHashCode;
			// !!!

			this.ClientResponseCacheMode = clientResponseCache.ClientResponseCacheMode;
			this.ClientResponseCacheUpdateIntervalSeconds = clientResponseCache.ClientResponseCacheUpdateIntervalSeconds;
		}

		public string CreateEndPointRequestPersistedHashCode()
		{
			string requestHashCode = string.Empty;

			string? requestHashCode_HttpHeaderValues = null;
			if (this.RequestHttpHeader
			    is
			    Dictionary<string, StringValues> requestHttpHeader)
			{
				var requestHttpHeadersForHashCode
				    = new Dictionary<string, StringValues>();
				var requestHttpHeaderHashKeys
				    = this.EndPointRequestHttpHeaderHashKeys?.Length > 0
				    ? this.EndPointRequestHttpHeaderHashKeys
				    : Config.Router.ClientRequestHttpHeaderHashKeys;
				if (requestHttpHeaderHashKeys?.Length > 0)
				{
					foreach (var httpHeaderHashKey
					    in
					    requestHttpHeaderHashKeys)
					{
						if (requestHttpHeader.TryGetValue(
						    httpHeaderHashKey,
						    out var httpHeaderValues)
						    && httpHeaderValues.Count > 0)
						{
							requestHttpHeadersForHashCode.Add(httpHeaderHashKey, httpHeaderValues);
						}
					}
				}
				foreach (var httpHeader
				    in
				    requestHttpHeadersForHashCode)
				{
					var httpHeaderKeyValue = httpHeader.Key + ":" + httpHeader.Value;
					if (requestHashCode_HttpHeaderValues == null)
					{
						requestHashCode_HttpHeaderValues = httpHeaderKeyValue;
					}
					else
					{
						requestHashCode_HttpHeaderValues += httpHeaderKeyValue;
					}
					requestHashCode_HttpHeaderValues += "\r\n";
				}
			}

			var endPointRequestUrlRelativePath
			    = EndPointRequestUrl?.GetPathInUri(true);
			var requestHashCode_RequestUrl_HeaderValues
			    = endPointRequestUrlRelativePath
			    + "\r\n"
			    + requestHashCode_HttpHeaderValues;
			var requestHashCode_RouteRequestUrl_HeaderValuesBytes
			    = requestHashCode_RequestUrl_HeaderValues
			    .ToUtf8Bytes();

			var requestBodyBytes
			    = RequestBodyBytes;
			var requestBodyBytesLength
			    = requestBodyBytes?.Length ?? 0;

			var requestHashCodePlaintextBytesLength
			    = requestHashCode_RouteRequestUrl_HeaderValuesBytes.Length
			    + requestBodyBytesLength;

			byte[]? requestHashCodePlaintextBytes = null;
			try
			{
				// var requestHashCodePlaintextBytes
				//     = new byte[requestHashCodePlaintextBytesLength];
				// !!!⚠ 
				// !!!⚠ 【注意】requestHashCodePlaintextBytes.length >=requestHashCodePlaintextBytesLength ⚠!!!
				// !!!⚠ 
				requestHashCodePlaintextBytes
				    = _bytesPool.Rent(requestHashCodePlaintextBytesLength);
				{
					Array.Copy(
					    requestHashCode_RouteRequestUrl_HeaderValuesBytes,
					    requestHashCodePlaintextBytes,
					    requestHashCode_RouteRequestUrl_HeaderValuesBytes.Length);
					if (requestBodyBytes != null
					    && requestBodyBytesLength > 0)
					{
						Array.Copy(
						    requestBodyBytes,
						    0,
						    requestHashCodePlaintextBytes,
						    requestHashCode_RouteRequestUrl_HeaderValuesBytes.Length,
						    requestBodyBytesLength);
					}
				}
				// !!!
				requestHashCode = requestHashCodePlaintextBytes.ToSHA256(
				    0,
				    requestHashCodePlaintextBytesLength);
				// !!!
			}
			finally
			{
				if (requestHashCodePlaintextBytes != null)
				{
					_bytesPool.Return(requestHashCodePlaintextBytes);
				}
			}

			return requestHashCode;
		}

		public int TryUpdateAndGetEndPointRequestHashCodeNumber()
		{
			if (_endPointRequestRunningHashCode != 0)
			{
				return _endPointRequestRunningHashCode;
			}

			EndPointRequestPersistedHashCode = CreateEndPointRequestPersistedHashCode();

			return _endPointRequestRunningHashCode;
		}


		#endregion
	}
}
