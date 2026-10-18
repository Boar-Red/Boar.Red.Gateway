using BaoXia.Utils.Extensions;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Extensions
{
	/// <summary>
	/// Http响应类扩展。
	/// </summary>
	public static class HttpResponseExtension
	{

		////////////////////////////////////////////////
		// @静态常量
		////////////////////////////////////////////////

		#region 静态常量

		public static readonly byte[] ResponseBodyEndBytes_ForBr = [3];

		static readonly HashSet<string> _hopByHopResponseHttpHeaderKeys = new(
			StringComparer.OrdinalIgnoreCase)
		{
			"Connection",
			"Keep-Alive",
			"Proxy-Connection",
			"TE",
			"Trailer",
			"Transfer-Encoding",
			"Upgrade"
		};

		#endregion



		////////////////////////////////////////////////
		// @类方法
		////////////////////////////////////////////////

		#region 类方法

		/// <summary>
		/// 将响应缓存写入响应。
		/// </summary>
		/// <param name="response">当前响应对象。</param>
		/// <param name="clientResponseCache">要写入的相应缓存对象。</param>
		/// <param name="routeStatisticsInfoRecorder">路由统计信息记录器。</param>
		public static async Task WriteResponseCacheForRequestAsync(
		    this HttpResponse response, HttpRequest request, ClientResponseCache clientResponseCache,
		    RouteStatisticsInfoRecorder routeStatisticsInfoRecorder)
		{
			if (request == null || clientResponseCache == null)
			{
				return;
			}

			// !!!
			response.StatusCode = clientResponseCache.ResponseHttpStatusCode;
			//

			var responseHttpHeader = clientResponseCache.ResponseHttpHeader;
			if (responseHttpHeader?.Count > 0)
			{
				foreach (var httpHeaderProperty in responseHttpHeader)
				{
					response.Headers.Append(
					    httpHeaderProperty.Key,
					    httpHeaderProperty.Value);
				}
			}

			var responseContentEncoding = clientResponseCache.ResponseContentEncoding;
			var responseBodyBytes = clientResponseCache.ResponseBodyBytes;
			byte[]? responseBodyBytesEndResponse = null;
			var responseBodyWriter = response.BodyWriter;
			if (responseBodyBytes?.Length > 0)
			{
				var responseBodyBytesCompressible = false;
				if (responseBodyBytes.Length
				    >= Config.Router.ClientResponseCompressionContentMinLength)
				{
					var responseContentType = clientResponseCache.ResponseContentType;
					var responseCompressionContentTypes
					    = Config.Router.ClientResponseCompressionContentTypes;
					if (responseContentType?.Length > 0
					    && responseCompressionContentTypes?.Length > 0)
					{
						foreach (var responseCompressionContentType
						    in
						    responseCompressionContentTypes)
						{
							if (responseContentType.Contains(responseCompressionContentType, StringComparison.OrdinalIgnoreCase))
							{
								// !!!
								responseBodyBytesCompressible = true;
								// !!!
								break;
							}
						}
					}
				}

				if (responseBodyBytesCompressible)
				{
					var responseBodyBytesNeedChangeCompresseMethod = false;
					var clientAcceptEncodings = request.Headers["Accept-Encoding"];
					var isClientCompressionEnable_Br = false;
					var isClientCompressionEnable_GZip = false;
					var isClientCompressionEnable_Deflate = false;

					if (clientAcceptEncodings.Count > 0)
					{
						// !!!
						responseBodyBytesNeedChangeCompresseMethod = true;
						// !!!
						foreach (var clientAcceptEncoding
						    in
						    clientAcceptEncodings)
						{
							if (clientAcceptEncoding.EqualsIgnoreCase(responseContentEncoding))
							{
								// !!!
								responseBodyBytesNeedChangeCompresseMethod = false;
								// !!!
								break;
							}
							if (clientAcceptEncoding?.IndexOf(
							    HttpHeaderValues.Content_Encodings.Br,
							    StringComparison.OrdinalIgnoreCase) >= 0)
							{
								isClientCompressionEnable_Br = true;
							}
							else if (clientAcceptEncoding?.IndexOf(
							    HttpHeaderValues.Content_Encodings.GZip,
							    StringComparison.OrdinalIgnoreCase) >= 0)
							{
								isClientCompressionEnable_GZip = true;
							}
							else if (clientAcceptEncoding?.IndexOf(
							    HttpHeaderValues.Content_Encodings.Deflate,
							    StringComparison.OrdinalIgnoreCase) >= 0)
							{
								isClientCompressionEnable_Deflate = true;
							}
						}
					}

					if (responseBodyBytesNeedChangeCompresseMethod)
					{
						if (ResponseCompressionMethods.Br.EqualsIgnoreCase(
						     Config.Router.ClientResponseCompressionMethod))
						{
							isClientCompressionEnable_Br = true;
							isClientCompressionEnable_GZip = false;
							isClientCompressionEnable_Deflate = false;
						}
						else if (ResponseCompressionMethods.GZip.EqualsIgnoreCase(
						     Config.Router.ClientResponseCompressionMethod))
						{
							isClientCompressionEnable_Br = false;
							isClientCompressionEnable_GZip = true;
							isClientCompressionEnable_Deflate = false;
						}
						else if (ResponseCompressionMethods.Deflate.EqualsIgnoreCase(
						     Config.Router.ClientResponseCompressionMethod))
						{
							isClientCompressionEnable_Br = false;
							isClientCompressionEnable_GZip = false;
							isClientCompressionEnable_Deflate = true;
						}

						////////////////////////////////////////////////
						////////////////////////////////////////////////
						////////////////////////////////////////////////
						// !!!
						if (isClientCompressionEnable_Br
						    && clientResponseCache.ResponseBodyBytesInBr?.Length > 0)
						{
							responseContentEncoding = HttpHeaderValues.Content_Encodings.Br;
							responseBodyBytes = clientResponseCache.ResponseBodyBytesInBr;
							// !!!
							responseBodyBytesEndResponse = ResponseBodyEndBytes_ForBr;
							// !!!
						}
						else if (isClientCompressionEnable_GZip
						    && clientResponseCache.ResponseBodyBytesInGZip?.Length > 0)
						{
							responseContentEncoding = HttpHeaderValues.Content_Encodings.GZip;
							responseBodyBytes = clientResponseCache.ResponseBodyBytesInGZip;
						}
						else if (isClientCompressionEnable_Deflate
						    && clientResponseCache.ResponseBodyBytesInDeflate?.Length > 0)
						{
							responseContentEncoding = HttpHeaderValues.Content_Encodings.Deflate;
							responseBodyBytes = clientResponseCache.ResponseBodyBytesInDeflate;
						}
						// !!!
						////////////////////////////////////////////////
						////////////////////////////////////////////////
						////////////////////////////////////////////////
					}
				}
			}

			////////////////////////////////////////////////
			////////////////////////////////////////////////
			////////////////////////////////////////////////
			// !!!
			response.Headers.Append(
			    Defines.Constants.HttpHeaderKeys.BaoXia_Gateway_EndPointName,
			    // !!! 编码终点名称避免，中文出错 !!!
			    clientResponseCache.EndPointName?.StringByEncodeInUriParam());
			response.Headers.Append(
			    Defines.Constants.HttpHeaderKeys.BaoXia_Gateway_ResponseType,
			    clientResponseCache.ResponseRouteResponseType);
			// !!!⚠ 注意返回响应后，立即标记路由响应的缓存模式为：HttpHeaderValues.RouteResponseTypes.CacheData ⚠!!!
			clientResponseCache.ResponseRouteResponseType
			       = HttpHeaderValues.RouteResponseTypes.CacheData;
			// !!!

			var routeDuration = routeStatisticsInfoRecorder.MillisecondsToResponse + "ms";
			response.Headers.Append(
			    Defines.Constants.HttpHeaderKeys.BaoXia_Gateway_RouteDuration,
			    routeDuration);
			// !!!
			////////////////////////////////////////////////
			////////////////////////////////////////////////
			////////////////////////////////////////////////

			response.Headers.Remove(
			    HttpHeaderKeys.Content_Encoding);
			response.Headers.Append(
			    HttpHeaderKeys.Content_Encoding,
			    responseContentEncoding);
			var responseBodyBytesLength = responseBodyBytes != null
			    ? responseBodyBytes.Length
			    : 0;
			response.Headers.ContentLength
			    = responseBodyBytesLength
			    + (responseBodyBytesEndResponse != null
			    ? responseBodyBytesEndResponse.Length
			    : 0);


			// #路由统计#
			routeStatisticsInfoRecorder.EndCreateResponseCache(response.StatusCode);


			// !!!
			if (responseBodyBytes != null)
			{
				// Log.Debug.Logs(response, "开始写入响应：" + responseBodyBytes.Length + "字节", clientResponseCache.RequestUrl);
				{
					await responseBodyWriter.WriteAsync(
					    new System.ReadOnlyMemory<byte>(responseBodyBytes));
					if (responseBodyBytesEndResponse?.Length > 0)
					{
						await responseBodyWriter.WriteAsync(
						    new System.ReadOnlyMemory<byte>(responseBodyBytesEndResponse));
					}
				}
				// Log.Debug.Logs(response, "结束写入响应：" + responseBodyBytes.Length + "字节", clientResponseCache.RequestUrl);
			}
			// !!!
		}

		/// <summary>
		/// 按指定字节数，将终点响应流式转发给客户端。
		/// </summary>
		public static async Task WriteEndPointResponseByBytesAsync(
			this HttpResponse response,
			HttpResponseMessage endPointResponseMessage,
			string? endPointName,
			int responseForwardBytesCount,
			RouteStatisticsInfoRecorder routeStatisticsInfoRecorder,
			CancellationToken cancellationToken)
		{
			await response.WriteEndPointResponseByBytesAndGetCompletionStateAsync(
				endPointResponseMessage,
				endPointName,
				responseForwardBytesCount,
				routeStatisticsInfoRecorder,
				cancellationToken);
		}

		/// <summary>
		/// 按指定字节数，将终点响应流式转发给客户端，并返回下游是否完整接收响应。
		/// </summary>
		internal static async Task<bool> WriteEndPointResponseByBytesAndGetCompletionStateAsync(
			this HttpResponse response,
			HttpResponseMessage endPointResponseMessage,
			string? endPointName,
			int responseForwardBytesCount,
			RouteStatisticsInfoRecorder routeStatisticsInfoRecorder,
			CancellationToken cancellationToken)
		{
			response.StatusCode = (int)endPointResponseMessage.StatusCode;

			var responseHttpHeaderValidKeys
				= Config.Router.ClientResponseHttpHeaderValidKeys?.Length > 0
				? [.. Config.Router.ClientResponseHttpHeaderValidKeys]
				: new HashSet<string>();
			var isAllResponseHttpHeaderKeyValid
				= responseHttpHeaderValidKeys.Count < 1;
			if (isAllResponseHttpHeaderKeyValid != true)
			{
				responseHttpHeaderValidKeys.Add(HttpHeaderKeys.Content_Type);
				responseHttpHeaderValidKeys.Add(HttpHeaderKeys.Content_Encoding);
			}

			void appendResponseHttpHeaders(
				IEnumerable<KeyValuePair<string, IEnumerable<string>>> responseHttpHeaders)
			{
				foreach (var responseHttpHeader in responseHttpHeaders)
				{
					var responseHttpHeaderKey = responseHttpHeader.Key;
					if (responseHttpHeaderKey.Equals(
						"Content-Length",
						StringComparison.OrdinalIgnoreCase)
						|| _hopByHopResponseHttpHeaderKeys.Contains(responseHttpHeaderKey)
						|| (isAllResponseHttpHeaderKeyValid != true
							&& responseHttpHeaderValidKeys.Contains(responseHttpHeaderKey) != true))
					{
						continue;
					}
					response.Headers.Append(
						responseHttpHeaderKey,
						new StringValues(responseHttpHeader.Value.ToArray()));
				}
			}

			appendResponseHttpHeaders(endPointResponseMessage.Headers);
			if (endPointResponseMessage.Content != null)
			{
				appendResponseHttpHeaders(endPointResponseMessage.Content.Headers);
				response.ContentLength = endPointResponseMessage.Content.Headers.ContentLength;
			}

			routeStatisticsInfoRecorder.EndCreateResponseCache(response.StatusCode);
			response.Headers.Append(
				Defines.Constants.HttpHeaderKeys.BaoXia_Gateway_EndPointName,
				endPointName?.StringByEncodeInUriParam());
			response.Headers.Append(
				Defines.Constants.HttpHeaderKeys.BaoXia_Gateway_ResponseType,
				HttpHeaderValues.RouteResponseTypes.OriginalData);
			response.Headers.Append(
				Defines.Constants.HttpHeaderKeys.BaoXia_Gateway_RouteDuration,
				routeStatisticsInfoRecorder.MillisecondsToResponse + "ms");

			if (endPointResponseMessage.Content == null)
			{
				return true;
			}

			await using var endPointResponseStream
				= await endPointResponseMessage.Content.ReadAsStreamAsync(cancellationToken);
			var responseBytesBufferLength = Math.Max(1, responseForwardBytesCount);
			var responseBytesBuffer = ArrayPool<byte>.Shared.Rent(responseBytesBufferLength);
			try
			{
				while (true)
				{
					var responseBytesCountRead = await endPointResponseStream.ReadAsync(
						responseBytesBuffer.AsMemory(0, responseBytesBufferLength),
						cancellationToken);
					if (responseBytesCountRead < 1)
					{
						return true;
					}

					FlushResult flushResult;
					try
					{
						flushResult = await response.BodyWriter.WriteAsync(
							responseBytesBuffer.AsMemory(0, responseBytesCountRead),
							cancellationToken);
					}
					catch (IOException)
					{
						return false;
					}
					if (flushResult.IsCanceled
						|| flushResult.IsCompleted)
					{
						return false;
					}
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(responseBytesBuffer);
			}
		}

		/// <summary>
		/// 将终点的 Server-Sent Events 响应实时转发给客户端。
		/// </summary>
		public static async Task<bool> WriteEndPointResponseWithServerSentEventsAsync(
			this HttpResponse response,
			HttpResponseMessage endPointResponseMessage,
			string? endPointName,
			int responseForwardBytesCount,
			RouteStatisticsInfoRecorder routeStatisticsInfoRecorder,
			CancellationToken cancellationToken)
		{
			response.StatusCode = (int)endPointResponseMessage.StatusCode;

			var responseHttpHeaderValidKeys
				= Config.Router.ClientResponseHttpHeaderValidKeys?.Length > 0
				? [.. Config.Router.ClientResponseHttpHeaderValidKeys]
				: new HashSet<string>();
			var isAllResponseHttpHeaderKeyValid
				= responseHttpHeaderValidKeys.Count < 1;
			if (isAllResponseHttpHeaderKeyValid != true)
			{
				responseHttpHeaderValidKeys.Add(HttpHeaderKeys.Content_Type);
				responseHttpHeaderValidKeys.Add(HttpHeaderKeys.Content_Encoding);
			}

			void appendResponseHttpHeaders(
				IEnumerable<KeyValuePair<string, IEnumerable<string>>> responseHttpHeaders)
			{
				foreach (var responseHttpHeader in responseHttpHeaders)
				{
					var responseHttpHeaderKey = responseHttpHeader.Key;
					if (responseHttpHeaderKey.Equals(
						"Content-Length",
						StringComparison.OrdinalIgnoreCase)
						|| _hopByHopResponseHttpHeaderKeys.Contains(responseHttpHeaderKey)
						|| (isAllResponseHttpHeaderKeyValid != true
							&& responseHttpHeaderValidKeys.Contains(responseHttpHeaderKey) != true))
					{
						continue;
					}
					response.Headers.Append(
						responseHttpHeaderKey,
						new StringValues(responseHttpHeader.Value.ToArray()));
				}
			}

			appendResponseHttpHeaders(endPointResponseMessage.Headers);
			if (endPointResponseMessage.Content != null)
			{
				appendResponseHttpHeaders(endPointResponseMessage.Content.Headers);
			}

			response.ContentLength = null;
			if (endPointResponseMessage.StatusCode == System.Net.HttpStatusCode.OK)
			{
				response.Headers["Cache-Control"] = "no-cache, no-transform";
				response.Headers["X-Accel-Buffering"] = "no";
			}
			response.HttpContext.Features
				.Get<IHttpResponseBodyFeature>()
				?.DisableBuffering();

			routeStatisticsInfoRecorder.EndCreateResponseCache(response.StatusCode);
			response.Headers.Append(
				Defines.Constants.HttpHeaderKeys.BaoXia_Gateway_EndPointName,
				endPointName?.StringByEncodeInUriParam());
			response.Headers.Append(
				Defines.Constants.HttpHeaderKeys.BaoXia_Gateway_ResponseType,
				HttpHeaderValues.RouteResponseTypes.OriginalData);
			response.Headers.Append(
				Defines.Constants.HttpHeaderKeys.BaoXia_Gateway_RouteDuration,
				routeStatisticsInfoRecorder.MillisecondsToResponse + "ms");

			// 先提交响应头，即使终点暂时没有事件，客户端也能立即建立 SSE 连接。
			try
			{
				await response.StartAsync(cancellationToken);
			}
			catch (IOException)
			{
				return false;
			}
			catch (OperationCanceledException)
			{
				return false;
			}
			FlushResult responseHeadersFlushResult;
			try
			{
				responseHeadersFlushResult
					= await response.BodyWriter.FlushAsync(cancellationToken);
			}
			catch (IOException)
			{
				return false;
			}
			catch (OperationCanceledException)
			{
				return false;
			}
			routeStatisticsInfoRecorder.EndSendResponse();
			if (responseHeadersFlushResult.IsCanceled
				|| responseHeadersFlushResult.IsCompleted)
			{
				return false;
			}
			if (endPointResponseMessage.Content == null)
			{
				return true;
			}

			await using var endPointResponseStream
				= await endPointResponseMessage.Content.ReadAsStreamAsync(cancellationToken);
			var responseBytesBufferLength = Math.Max(1, responseForwardBytesCount);
			var responseBytesBuffer = ArrayPool<byte>.Shared.Rent(responseBytesBufferLength);
			try
			{
				while (true)
				{
					var responseBytesCountRead = await endPointResponseStream.ReadAsync(
						responseBytesBuffer.AsMemory(0, responseBytesBufferLength),
						cancellationToken);
					if (responseBytesCountRead < 1)
					{
						return true;
					}

					FlushResult flushResult;
					try
					{
						flushResult = await response.BodyWriter.WriteAsync(
							responseBytesBuffer.AsMemory(0, responseBytesCountRead),
							cancellationToken);
					}
					catch (IOException)
					{
						return false;
					}
					catch (OperationCanceledException)
					{
						return false;
					}
					if (flushResult.IsCanceled
						|| flushResult.IsCompleted)
					{
						return false;
					}
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(responseBytesBuffer);
			}
		}

		#endregion
	}
}
