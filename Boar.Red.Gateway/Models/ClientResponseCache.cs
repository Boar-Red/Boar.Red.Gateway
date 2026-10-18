using BaoXia.Utils.Extensions;
using BaoXia.Utils.Models;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Extensions;
using Boar.Red.Gateway.LogFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Models
{
	[Index("RequestHashCode")]
	public class ClientResponseCache : EndPointRequestInfo
	{
		////////////////////////////////////////////////
		// @静态常量
		////////////////////////////////////////////////

		#region 静态常量

		public static readonly ClientResponseInfo ClientResponseCacheWithEndPointRequestInfo =
			new(
				ResponseExceptionProcess.ReturnExceptionInfo,
				//
				ResponseCacheMode.NoneCache,
				0.0,
				DateTime.MaxValue,
				//
				(int)HttpStatusCode.NotFound,
				new Dictionary<string, StringValues>
				{
					{
						HttpHeaderKeys.Content_Type,
						HttpHeaderValues.Content_Types.Text_Plain_Utf8
					},
					{
						HttpHeaderKeys.Content_Encoding,
						HttpHeaderValues.Content_Encodings.Identity
					},
				},
				ResponseBodies.Http_404_CreateResponseCacheFailed.ToUtf8Bytes()
			);

		#endregion


		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		////////////////////////////////////////////////
		////////////////////////////////////////////////
		////////////////////////////////////////////////

		#region 缓存相关属性

		/// <summary>
		/// 更新时间。
		/// </summary>
		public DateTime LastUpdateTime { get; set; } = DateTime.MinValue;

		/// <summary>
		/// 响应内容下次更新时间。
		/// </summary>
		public DateTime NextUpdateTime
		{
			get
			{
				DateTime nextUpdateTime;
				if (this.ClientResponseCacheUpdateIntervalSeconds > 0)
				{
					nextUpdateTime = this.LastUpdateTime.AddSeconds(
						this.ClientResponseCacheUpdateIntervalSeconds
					);
				}
				else
				{
					nextUpdateTime = DateTime.MaxValue;
				}
				return nextUpdateTime;
			}
		}

		/// <summary>
		/// 上一次持久化时间。
		/// </summary>
		public DateTime StorageTime { get; set; } = DateTime.MinValue;

		/// <summary>
		/// 响应内容最后一次响应的时间。
		/// </summary>
		public DateTime LastResponseTime { get; set; } = DateTime.Now;

		/// <summary>
		/// 当前响应缓存是否需要更新。
		/// </summary>
		public bool IsNeedToUpdate
		{
			get
			{
				// 响应内容已过期：
				if (
					this.IsNeedSaveResponseCache
					&& this.NextUpdateTime <= DateTime.Now
				)
				{
					return true;
				}
				return false;
			}
		}

		/// <summary>
		/// 当前响应缓存是否处于更新队列。
		/// </summary>
		[NotMapped]
		public bool IsInUpdateQueue { get; set; }

		/// <summary>
		/// 当前响应缓存是否需要被持久化。
		/// </summary>
		public bool IsNeedToStorage
		{
			get
			{
				// 响应内容已过期：
				if (
					(
						this.IsNeedSaveResponseCache
						|| this.EndPointResponseExceptionProcess
							== ResponseExceptionProcess.ReturnLastResponseCache
					)
					&& this.LastUpdateTime > this.StorageTime
				)
				{
					return true;
				}
				return false;
			}
		}

		/// <summary>
		/// 当前响应缓存是否处于持久化队列。
		/// </summary>
		[NotMapped]
		public bool IsInStorageQueue { get; set; }

		#endregion


		////////////////////////////////////////////////
		////////////////////////////////////////////////
		////////////////////////////////////////////////

		#region 响应相关属性

		public int ResponseHttpStatusCode { get; set; }

		[NotMapped]
		public string? ResponseRouteResponseType { get; set; }

		protected string? _responseHttpHeaderString;
		protected Dictionary<string, StringValues>? _responseHttpHeader;

		/// <summary>
		/// 响应的Http头。
		/// </summary>
		public string? ResponseHttpHeaderString
		{
			get { return _responseHttpHeaderString; }
			set
			{
				if (_responseHttpHeaderString != value)
				{
					_responseHttpHeaderString = value;
					_responseHttpHeader = ClientResponseCache.CreateHttpHeader(
						_responseHttpHeaderString
					);
				}
			}
		}

		/// <summary>
		/// 响应的Http头。
		/// </summary>
		[NotMapped]
		public Dictionary<string, StringValues>? ResponseHttpHeader
		{
			get { return _responseHttpHeader; }
			set
			{
				if (_responseHttpHeader != value)
				{
					_responseHttpHeader = value;
					_responseHttpHeaderString =
						ClientResponseCache.CreateHttpHeaderString(
							_responseHttpHeader
						);
				}
			}
		}

		/// <summary>
		/// 响应内容的内容类型。
		/// </summary>
		public string? ResponseContentType
		{
			get
			{
				if (
					this.ResponseHttpHeader?.TryGetValue(
						HttpHeaderKeys.Content_Type,
						out var contentType
					) == true
				)
				{
					return contentType.ToString();
				}
				return null;
			}
		}

		/// <summary>
		/// 响应内容的内容编码方式。
		/// </summary>
		public string? ResponseContentEncoding
		{
			get
			{
				if (
					this.ResponseHttpHeader?.TryGetValue(
						HttpHeaderKeys.Content_Encoding,
						out var contentEncoding
					) == true
				)
				{
					return contentEncoding.ToString();
				}
				return null;
			}
		}

		protected byte[]? _responseBodyBytes;
		protected byte[]? _responseBodyBytesInPlaintext;
		protected byte[]? _responseBodyBytesInBr;
		protected byte[]? _responseBodyBytesInGZip;
		protected byte[]? _responseBodyBytesInDeflate;

		/// <summary>
		/// 响应主体内容。
		/// </summary>
		public byte[]? ResponseBodyBytes
		{
			set
			{
				if (_responseBodyBytes != value)
				{
					_responseBodyBytes = value;
					_responseBodyBytesInPlaintext = null;
					_responseBodyBytesInBr = null;
					_responseBodyBytesInGZip = null;
					_responseBodyBytesInDeflate = null;
				}
			}
			get { return _responseBodyBytes; }
		}

		/// <summary>
		/// 响应主体内容，没有压缩的铭文。
		/// </summary>
		[NotMapped]
		public byte[]? ResponseBodyBytesInPlaintext
		{
			get
			{
				if (_responseBodyBytesInPlaintext == null)
				{
					var responseBodyBytes = this.ResponseBodyBytes;
					if (responseBodyBytes?.Length > 0)
					{
						// !!!
						_responseBodyBytesInPlaintext = responseBodyBytes;
						// !!!
						var responseContentEncoding =
							this.ResponseContentEncoding;
						if (
							ResponseCompressionMethods.Br.EqualsIgnoreCase(
								responseContentEncoding
							)
						)
						{
							_responseBodyBytesInPlaintext =
								responseBodyBytes
									.BytesByDecompressWithBrotli()
									?.ToArray();
						}
						else if (
							ResponseCompressionMethods.GZip.EqualsIgnoreCase(
								responseContentEncoding
							)
						)
						{
							_responseBodyBytesInPlaintext =
								responseBodyBytes
									.BytesByDecompressWithGZip()
									?.ToArray();
						}
						else if (
							ResponseCompressionMethods.Deflate.EqualsIgnoreCase(
								responseContentEncoding
							)
						)
						{
							_responseBodyBytesInPlaintext =
								responseBodyBytes
									.BytesByDecompressWithDeflate()
									?.ToArray();
						}
					}
				}
				return _responseBodyBytesInPlaintext;
			}
		}

		/// <summary>
		/// 响应主体内容，使用“Br”压缩。
		/// </summary>
		[NotMapped]
		public byte[]? ResponseBodyBytesInBr
		{
			get
			{
				if (_responseBodyBytesInBr == null)
				{
					var responseBodyBytesInBr =
						this.ResponseBodyBytesInPlaintext?.BytesByCompressWithBrotli();
					if (responseBodyBytesInBr?.Length > 0)
					{
						responseBodyBytesInBr =
							responseBodyBytesInBr.ArrayByAdd(
								//!!!⚠ 注意这里，Brotli压缩后，要增加“3”（ascii码：end of text）作为内容结尾， ⚠!!!
								//!!!⚠ 否则一些软件，如：Postman，无法正确解压。 ⚠!!!
								(byte)3
							);
					}
					// !!!
					_responseBodyBytesInBr = responseBodyBytesInBr;
					// !!!
				}
				return _responseBodyBytesInBr;
			}
		}

		/// <summary>
		/// 响应主体内容，使用“GZip”压缩。
		/// </summary>
		[NotMapped]
		public byte[]? ResponseBodyBytesInGZip
		{
			get
			{
				// !!!
				_responseBodyBytesInGZip ??=
					this.ResponseBodyBytesInPlaintext?.BytesByCompressWithGZip();
				// !!!
				return _responseBodyBytesInGZip;
			}
		}

		/// <summary>
		/// 响应主体内容，使用“GZip”压缩。
		/// </summary>
		[NotMapped]
		public byte[]? ResponseBodyBytesInDeflate
		{
			get
			{
				// !!!
				_responseBodyBytesInDeflate ??=
					this.ResponseBodyBytesInPlaintext?.BytesByCompressWithDeflate();
				// !!!
				return _responseBodyBytesInDeflate;
			}
		}

		#endregion


		////////////////////////////////////////////////
		// @类方法
		////////////////////////////////////////////////

		#region 类方法


		/// <summary>
		/// 中转请求，更新请求响应缓存。
		/// </summary>
		/// <param name="clientResponseCache">要更新的响应缓存。</param>
		/// <param name="endRequestInfo">请求的哈希信息。</param>
		/// <param name="clientIp">客户端的Ip地址。</param>
		/// <returns>经过路由请求后的响应缓存对象。</returns>
		protected static async Task UpdateResponseCacheWithEndPointRequestInfoAsync(
			ClientResponseCache clientResponseCache, EndPointRequestInfo endRequestInfo, ConnectionIpEndPoints? connectionIpEndPoints)
		{
			if (clientResponseCache == null || endRequestInfo == null)
			{
				return;
			}

			try
			{
				ClientResponseInfo? clientResponseInfo = null;
				{
					var routeRequestMessage = ClientResponseCache.CreateRequestMessageToUrl(
						endRequestInfo.EndPointRequestUrl, endRequestInfo.RequestHttpHeader, endRequestInfo.RequestBodyBytes,
						connectionIpEndPoints);
					if (routeRequestMessage != null)
					{
						clientResponseInfo =
							await ClientResponseCaches.HttpClient.GetClientResponseInfoAsync(
								routeRequestMessage,
								endRequestInfo.EndPointRequestTimeoutSeconds,
								//
								endRequestInfo.EndPointResponseExceptionProcess,
								//
								endRequestInfo.ClientResponseCacheMode,
								endRequestInfo.ClientResponseCacheUpdateIntervalSeconds
							);
					}
				}
				clientResponseInfo ??=
					ClientResponseCache.ClientResponseCacheWithEndPointRequestInfo;

				clientResponseCache.UpdateForEndPointRequestHashCode(
					endRequestInfo.EndPointRequestPersistedHashCode,
					endRequestInfo.EndPointRequestRunningHashCode,
					//
					clientResponseInfo.EndPointResponseExceptionProcess,
					//
					clientResponseInfo.CacheMode,
					clientResponseInfo.CacheUpdateIntervalSeconds,
					//
					endRequestInfo.RequestPath,
					endRequestInfo.EndPointRequestUrl,
					endRequestInfo.RequestHttpHeader,
					endRequestInfo.RequestBodyBytes,
					//
					clientResponseInfo.ResponseHttpStatusCode,
					HttpHeaderValues.RouteResponseTypes.OriginalData,
					clientResponseInfo.ResponseHttpHeader,
					clientResponseInfo.ResponseBodyBytes
				);
			}
			catch (Exception exception)
			{
				Log.Exception.Logs(
					nameof(ClientResponseCache), $"更新响应内容（{endRequestInfo.EndPointRequestUrl}）失败，程序异常。", exception);

				////////////////////////////////////////////////
				// !!!⚠ 返回异常信息 ⚠!!!
				////////////////////////////////////////////////

				clientResponseCache.ResponseHttpStatusCode = (int)
					System.Net.HttpStatusCode.InternalServerError;
				clientResponseCache.ResponseHttpHeader = new Dictionary<
					string,
					StringValues
				>
				{
					{
						"Content-Type",
						HttpHeaderValues.Content_Types.Text_Plain_Utf8
					},
				};
				clientResponseCache.ResponseBodyBytes = exception
					.ToString()
					.ToUtf8Bytes();

				throw;
			}
		}

		/// <summary>
		/// 中转请求，更新请求响应缓存。
		/// </summary>
		/// <param name="clientResponseCache">要更新的响应缓存。</param>
		/// <param name="clientIp">客户端的Ip地址。</param>
		/// <param name="isUpdateIgnoreResponseStatus">是否忽略响应状态的更新。</param>
		/// <returns>更新后的响应缓存。</returns>
		public static async Task UpdateResponseCacheAsync(
			ClientResponseCache clientResponseCache, ConnectionIpEndPoints? connectionIpEndPoints)
		{
			if (clientResponseCache == null)
			{
				return;
			}
			await ClientResponseCache.UpdateResponseCacheWithEndPointRequestInfoAsync(
				clientResponseCache, new EndPointRequestInfo(clientResponseCache), connectionIpEndPoints);
		}

		/// <summary>
		/// 中转请求，返回路由后的请求响应。
		/// </summary>
		/// <param name="endRequestInfo">请求的哈希信息。</param>
		/// <param name="clientIp">客户端的Ip地址。</param>
		/// <returns>经过路由请求后的响应缓存对象。</returns>
		public static async Task<ClientResponseCache> CreateResponseCacheWithEndPointRequestInfoAsync(
			EndPointRequestInfo endRequestInfo, ConnectionIpEndPoints? connectionIpEndPoints)
		{
			var clientResponseCache = new ClientResponseCache();
			{
				await ClientResponseCache.UpdateResponseCacheWithEndPointRequestInfoAsync(
					clientResponseCache, endRequestInfo, connectionIpEndPoints);
			}
			return clientResponseCache;
		}

		#endregion


		////////////////////////////////////////////////
		// @自身实现
		////////////////////////////////////////////////

		/// <summary>
		/// 默认构造函数。
		/// </summary>
		public ClientResponseCache() { }

		/// <summary>
		/// 创建响应缓存请求。
		/// </summary>
		/// <param name="routeRequestUrl">目标路由请求Url。</param>
		/// <param name="requestIdentityInfo">当前请求身份信息。</param>
		/// <param name="clientIp">当前客户端Ip地址。</param>
		/// <returns>当前请求对应的路由请求对象。</returns>
		public static HttpRequestMessage? CreateRequestMessageToUrl(
			string? requestUrl, Dictionary<string, StringValues>? requestHttpHeaders,
			byte[]? requestBodyBytes, ConnectionIpEndPoints? connectionIpEndPoints)
		{
			if (requestUrl == null || requestUrl.Length < 1)
			{
				return null;
			}

			var requestMessage = new HttpRequestMessage()
			{
				RequestUri = new Uri(requestUrl),
			};
			{
				////////////////////////////////////////////////
				// 1/2，构建请求主体：
				////////////////////////////////////////////////
				ByteArrayContent? requestContent = null;
				if (requestBodyBytes?.Length > 0)
				{
					requestContent = new ByteArrayContent(requestBodyBytes);
					requestMessage.Content = requestContent;
				}

				////////////////////////////////////////////////
				// 2/2，构建请求头：
				////////////////////////////////////////////////

				if (requestHttpHeaders?.Count > 0)
				{
					// !!! 客户端Ip字段 !!!
					if (connectionIpEndPoints != null)
					{
						var clientConnectionIpEndPointsString = connectionIpEndPoints.IpEndPoints
							.ToClientConnectionIpEndPointsString();
						if (clientConnectionIpEndPointsString?.Length > 0)
						{
							var clientConnectionIpEndPointsStringEncoded = Uri.EscapeDataString(clientConnectionIpEndPointsString);
							requestMessage.Headers.TryAddWithoutValidation(
								Defines.Constants.HttpHeaderKeys.BaoXia_Gateway_ConnectionIpEndPoints,
								clientConnectionIpEndPointsStringEncoded);
						}
						else
						{
							requestMessage.Headers.Remove(Defines.Constants.HttpHeaderKeys.BaoXia_Gateway_ConnectionIpEndPoints);
						}

						if (connectionIpEndPoints.KeyIpEndPoint is { } keyIpEndPoint
							&& !requestMessage.Headers.Contains(HttpHeaderKeys.X_Real_IP))
						{
							var lastIPEndPointAddress = keyIpEndPoint.Address.ToString();
							var lastIPEndPointAddressEncoded = Uri.EscapeDataString(lastIPEndPointAddress);
							requestMessage.Headers.TryAddWithoutValidation(HttpHeaderKeys.X_Real_IP, lastIPEndPointAddressEncoded);
						}
					}
					else
					{
						requestMessage.Headers.Remove(Defines.Constants.HttpHeaderKeys.BaoXia_Gateway_ConnectionIpEndPoints);
					}

					foreach (var requestHttpHeader in requestHttpHeaders)
					{
						if (
							Constants.HttpHeaderKeys.Method.EqualsIgnoreCase(
								requestHttpHeader.Key
							)
						)
						{
							string? requestHttpMethod =
								requestHttpHeader.Value;
							if (requestHttpMethod != null)
							{
								requestMessage.Method =
									new HttpMethod(
										requestHttpMethod
									);
							}
						}
						else if (
							Constants.HttpHeaderKeys.Content_Type.EqualsIgnoreCase(
								requestHttpHeader.Key
							)
						)
						{
							if (requestContent != null)
							{
								if (
									MediaTypeHeaderValue.TryParse(
										requestHttpHeader.Value,
										out var requestContentType
									)
								)
								{
									requestContent
										.Headers
										.ContentType =
										requestContentType;
								}
							}
						}
						else
						{
							// !!!
							var headerValues = requestHttpHeader.Value;
							if (headerValues.Count > 0)
							{
								var headerValuesEncoded =
									headerValues
										.ToString()
										?.StringByEncodeUnasciiCharsToUriParam();
								//requestMessage.Headers.Add(
								//        requestHttpHeader.Key,
								//        headerValuesEncoded);
								requestMessage.Headers.TryAddWithoutValidation(
									requestHttpHeader.Key,
									headerValuesEncoded
								);
							}
							// !!!
						}
					}
				}
			}
			return requestMessage;
		}

		public void UpdateByRequestShotForRequestInfo(
			EndPointRequestInfo endPointRequestInfo,
			ClientResponseCacheCreateParam? clientResponseCacheCreateParam,
			bool isClientResponseNeedAsyncCache,
			in ConcurrentBag<ClientResponseCacheAsyncCacheInfo> clientResponseCachesAsyncCache,
			in ConcurrentBag<ClientResponseCache> clientResponseCachesNeedToUpdate,
			in ConcurrentBag<ClientResponseCache> clientResponseCachesNeedToStorage
		)
		{
			// 1/3，更新，缓存基础属性：
			{
				this.EndPointName = endPointRequestInfo.EndPointName;
				this.EndPointResponseExceptionProcess =
					endPointRequestInfo.EndPointResponseExceptionProcess;

				this.ClientResponseCacheMode =
					endPointRequestInfo.ClientResponseCacheMode;
				this.ClientResponseCacheUpdateIntervalSeconds =
					endPointRequestInfo.ClientResponseCacheUpdateIntervalSeconds;
			}

			// 2/3，更新，响应时间：
			this.LastResponseTime = DateTime.Now;

			// 3/3，进入对应的处理队列：
			if (isClientResponseNeedAsyncCache == true)
			{
				clientResponseCachesAsyncCache.Add(
					new(this, clientResponseCacheCreateParam)
				);
			}
			if (this.IsNeedToUpdate && !this.IsInUpdateQueue)
			{
				// !!!
				this.IsInUpdateQueue = true;
				// !!!
				clientResponseCachesNeedToUpdate.Add(this);
			}
			if (this.IsNeedToStorage && !this.IsInStorageQueue)
			{
				// !!!
				this.IsInStorageQueue = true;
				// !!!
				clientResponseCachesNeedToStorage.Add(this);
			}
		}

		public void UpdateForEndPointRequestHashCode(
			string? endPointRequestPersistedHashCode,
			int endPointRequestRunningHashCode,
			//
			ResponseExceptionProcess endPointResponseExceptionProcess,
			//
			ResponseCacheMode clientResponseCacheMode,
			double clientResponseCacheUpdateIntervalSeconds,
			//
			string? clientRequestPath,
			string? endPointRequestUrl,
			Dictionary<string, StringValues>? endPointRequestHttpHeader,
			byte[]? endPointRequestBodyBytes,
			//
			int responseHttpStatusCode,
			string responseRouteResponseType,
			Dictionary<string, StringValues>? responseHttpHeader,
			byte[]? responseBodyBytes
		)
		{
			_endPointRequestPersistedHashCode = endPointRequestPersistedHashCode;
			_endPointRequestRunningHashCode = endPointRequestRunningHashCode;

			this.EndPointResponseExceptionProcess = endPointResponseExceptionProcess;

			this.ClientResponseCacheMode = clientResponseCacheMode;
			this.ClientResponseCacheUpdateIntervalSeconds =
				clientResponseCacheUpdateIntervalSeconds;

			// !!!
			this.LastUpdateTime = DateTime.Now;
			// !!!

			this.RequestPath = clientRequestPath;
			this.EndPointRequestUrl = endPointRequestUrl;
			this.RequestHttpHeader = endPointRequestHttpHeader;
			this.RequestBodyBytes = endPointRequestBodyBytes;

			this.ResponseHttpStatusCode = responseHttpStatusCode;
			this.ResponseRouteResponseType = responseRouteResponseType;
			this.ResponseHttpHeader = responseHttpHeader;
			this.ResponseBodyBytes = responseBodyBytes;
		}

		public async Task UpdateAsync()
		{
			await ClientResponseCache.UpdateResponseCacheAsync(this, null);
		}
	}
}
