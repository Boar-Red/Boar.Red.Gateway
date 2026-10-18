using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Models;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Extensions
{
	public static class HttpClientExtension
	{
		//static readonly CancellationTokenSource _timeoutCancellationTokenSource = new();

		/// <summary>
		/// 由当前响应创建响应缓存。
		/// </summary>
		/// <param name="httpClient">当前Http客户端对象。</param>
		/// <param name="requestMessage">要发送的Http消息对象。</param>
		/// <param name="timeoutSeconds">当前请求的超时秒数。</param>
		/// <param name="endPointResponseExceptionProcess">当前响应异常时的处理方法。</param>
		/// <param name="cacheMode">当前响应缓存模式。</param>
		/// <param name="updateIntervalSeconds">当前响应缓存更新间隔秒数。</param>
		/// <returns>终点响应信息对象。</returns>
		public static async Task<ClientResponseInfo?> GetClientResponseInfoAsync(
		    this HttpClient httpClient,
		    HttpRequestMessage requestMessage,
		    double timeoutSeconds,
		    //
		    ResponseExceptionProcess endPointResponseExceptionProcess,
		    //
		    ResponseCacheMode cacheMode,
		    double cacehUpdateIntervalSeconds)
		{
			if (httpClient == null
			    || requestMessage == null)
			{
				return null;
			}

			////////////////////////////////////////////////
			// 1/4，网络请求：
			////////////////////////////////////////////////

			using var timeoutCancellationTokenSource
			    = timeoutSeconds > 0
			    ? new CancellationTokenSource()
			    : null;
			{
				timeoutCancellationTokenSource?.CancelAfter((int)(1000.0 * timeoutSeconds));
			}

			// !!!
			var responseMessage
			    = timeoutCancellationTokenSource != null
			    ? await httpClient.SendAsync(requestMessage, timeoutCancellationTokenSource.Token)
			    : await httpClient.SendAsync(requestMessage);
			// !!!
			if (responseMessage == null)
			{
				return null;
			}


			////////////////////////////////////////////////
			// 1/3，获取响应内容的Http头部属性：
			////////////////////////////////////////////////

			Dictionary<string, StringValues> responseHttpHeader = new();
			var responseHttpHeaderValidKeys
			    = Config.Router.ClientResponseHttpHeaderValidKeys?.Length > 0
			    ? [.. Config.Router.ClientResponseHttpHeaderValidKeys]
			    : new HashSet<string>();
			var isAllResponseHttpHeaderKeyValid
			    = responseHttpHeaderValidKeys.Count < 1;
			if (isAllResponseHttpHeaderKeyValid != true)
			{
				// !!!⚠ 填充默认的有效响应头 ⚠!!!：
				// Content-Type，Content-Encoding
				responseHttpHeaderValidKeys.Add(HttpHeaderKeys.Content_Type);
				responseHttpHeaderValidKeys.Add(HttpHeaderKeys.Content_Encoding);
			}
			//
			{
				string? httpHeaderValueFirst = null;
				List<string>? httpHeaderValueList = null;

				// 1/2，采集“HttpHeader”：
				if (responseMessage.Headers is var responseHeaders)
				{
					if (isAllResponseHttpHeaderKeyValid)
					{
						foreach (var responseHeader in responseHeaders)
						{
							// !!!
							responseHttpHeaderValidKeys.Add(responseHeader.Key);
							// !!!
						}
					}
					foreach (var httpHeaderKey in responseHttpHeaderValidKeys)
					{
						if (responseHeaders.TryGetValues(
						    httpHeaderKey,
						    out var httpHeaderValues))
						{
							// !!!
							httpHeaderValueFirst = null;
							httpHeaderValueList?.Clear();
							// !!!
							foreach (var httpHeaderValue in httpHeaderValues)
							{
								if (httpHeaderValue?.Length > 0)
								{
									if (httpHeaderValueFirst == null)
									{
										httpHeaderValueFirst = httpHeaderValue;
									}
									else
									{
										httpHeaderValueList ??= new List<string>();
										httpHeaderValueList.Add(httpHeaderValue);
									}
								}
							}

							if (httpHeaderValueList?.Count > 0)
							{
								// !!!
								if (httpHeaderValueFirst != null)
								{
									httpHeaderValueList.Insert(0, httpHeaderValueFirst);
								}
								// !!!
								responseHttpHeader.Add(
								    httpHeaderKey,
								    new StringValues(httpHeaderValueList.ToArray()));
								// !!!
							}
							else if (httpHeaderValueFirst != null)
							{
								// !!!
								responseHttpHeader.Add(
								    httpHeaderKey,
								    httpHeaderValueFirst);
								// !!!
							}
						}
					}
				}
				// 2/2，采集“ContentHeader”：
				if (responseMessage.Content?.Headers is HttpContentHeaders contentHeaders)
				{
					if (isAllResponseHttpHeaderKeyValid)
					{
						foreach (var responseHeader in contentHeaders)
						{
							// !!!
							responseHttpHeaderValidKeys.Add(responseHeader.Key);
							// !!!
						}
					}
					foreach (var httpHeaderKey in responseHttpHeaderValidKeys)
					{
						if (contentHeaders.TryGetValues(
						    httpHeaderKey,
						    out var httpHeaderValues))
						{
							// !!!
							httpHeaderValueFirst = null;
							httpHeaderValueList?.Clear();
							// !!!
							foreach (var httpHeaderValue in httpHeaderValues)
							{
								if (httpHeaderValue?.Length > 0)
								{
									if (httpHeaderValueFirst == null)
									{
										httpHeaderValueFirst = httpHeaderValue;
									}
									else
									{
										httpHeaderValueList ??= new List<string>();
										httpHeaderValueList.Add(httpHeaderValue);
									}
								}
							}
							if (httpHeaderValueList?.Count > 0)
							{
								// !!!
								if (httpHeaderValueFirst != null)
								{
									httpHeaderValueList.Insert(0, httpHeaderValueFirst);
								}
								// !!!
								responseHttpHeader.Add(
								    httpHeaderKey,
								    new StringValues(httpHeaderValueList.ToArray()));
								// !!!
							}
							else if (httpHeaderValueFirst != null)
							{
								// !!!
								responseHttpHeader.Add(
								    httpHeaderKey,
								    httpHeaderValueFirst);
								// !!!
							}
						}
					}
				}
			}


			////////////////////////////////////////////////
			// 2/3，获取响应内容的字节数组：
			////////////////////////////////////////////////

			byte[]? responseBodyBytes = null;
			var responseContent = responseMessage.Content;
			if (responseContent != null)
			{
				var beginTime = DateTime.Now;

				// !!!
				responseBodyBytes = timeoutCancellationTokenSource != null
				? await responseContent.ReadAsByteArrayAsync(timeoutCancellationTokenSource.Token)
				: await responseContent.ReadAsByteArrayAsync();
				// !!!

				var endTime = DateTime.Now;
				//System.Diagnostics.Debug.WriteLine("使用“ReadAsByteArrayAsync”获取响应，耗时 " + (endTime - beginTime).TotalSeconds.ToString("F3") + " 秒。");
			}

			////////////////////////////////////////////////
			// 4/4，返回响应信息：
			////////////////////////////////////////////////

			ClientResponseInfo clientResponseInfo = new(
			    endPointResponseExceptionProcess,
			    //
			    cacheMode,
			    cacehUpdateIntervalSeconds,
			    //
			    cacehUpdateIntervalSeconds <= 0
			    ? DateTime.MaxValue
			    : DateTime.Now.AddSeconds(cacehUpdateIntervalSeconds),
			    //
			    (int)responseMessage.StatusCode,
			    responseHttpHeader,
			    responseBodyBytes);
			{ }
			return clientResponseInfo;
		}
	}
}
