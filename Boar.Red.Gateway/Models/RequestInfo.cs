using Boar.Red.Gateway.Constants;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Boar.Red.Gateway.Models
{
	public class RequestInfo
	{
		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性

		////////////////////////////////////////////////
		////////////////////////////////////////////////
		////////////////////////////////////////////////

		#region 请求相关属性

		/// <summary>
		/// 客户端的请求路径（相对）。
		/// </summary>
		public string? RequestPath { get; set; }

		/// <summary>
		/// 客户端的查询参数。
		/// </summary>
		public string? RequestQueryString { get; set; }


		/// <summary>
		/// 请求的Http头字符串。
		/// </summary>
		public string? RequestHttpHeaderString { get; set; }

		protected Dictionary<string, StringValues>? _requestHttpHeader;

		/// <summary>
		/// 请求的Http头。
		/// </summary>
		[NotMapped]
		public Dictionary<string, StringValues>? RequestHttpHeader
		{
			get
			{
				if (_requestHttpHeader == null
				    && this.RequestHttpHeaderString?.Length > 0)
				{
					_requestHttpHeader = RequestInfo.CreateHttpHeader(this.RequestHttpHeaderString);
				}
				return _requestHttpHeader;
			}
			set
			{
				if (_requestHttpHeader != value)
				{
					_requestHttpHeader = value;
					this.RequestHttpHeaderString = RequestInfo.CreateHttpHeaderString(_requestHttpHeader);
				}
			}
		}

		/// <summary>
		/// 请求主体内容。
		/// </summary>
		public byte[]? RequestBodyBytes { get; set; }

		#endregion



		////////////////////////////////////////////////
		// @类方法
		////////////////////////////////////////////////

		#region 类方法

		protected static string? CreateHttpHeaderString(Dictionary<string, StringValues>? httpHeader)
		{
			string? httpHeaderString = null;
			if (httpHeader?.Count > 0)
			{
				foreach (var httpHeaderProperty in httpHeader)
				{
					var httpHeaderPropertyString
					    = httpHeaderProperty.Key
					    + HttpHeader.SpliterBetweenKeyAndValue
					    + httpHeaderProperty.Value;
					if (httpHeaderString == null)
					{
						httpHeaderString = httpHeaderPropertyString;
					}
					else
					{
						httpHeaderString
						    += HttpHeader.SpliterBetweenProperties
						    + httpHeaderPropertyString;
					}
				}
			}
			return httpHeaderString;
		}

		protected static Dictionary<string, StringValues>? CreateHttpHeader(string? httpHeaderString)
		{
			Dictionary<string, StringValues>? httpHeader = null;
			if (httpHeaderString?.Length > 0)
			{
				var httpHeaderPropertyStrings = httpHeaderString.Split(
				    HttpHeader.SpliterBetweenProperties,
				    StringSplitOptions.RemoveEmptyEntries);
				if (httpHeaderPropertyStrings?.Length > 0)
				{
					foreach (var httpHeaderPropertyString
					    in
					    httpHeaderPropertyStrings)
					{
						var httpHeaderPropertyKeyValue
						    = httpHeaderPropertyString.Split(
						    HttpHeader.SpliterBetweenKeyAndValue,
						    StringSplitOptions.RemoveEmptyEntries);
						string? httpHeaderPropertyKey = null;
						string? httpHeaderPropertyValue = null;
						if (httpHeaderPropertyKeyValue?.Length > 0)
						{
							httpHeaderPropertyKey = httpHeaderPropertyKeyValue[0].Trim();
							if (httpHeaderPropertyKey.Length > 0
							    && httpHeaderPropertyKeyValue.Length > 1)
							{
								for (var httpHeaderPropertyKeyValueIndex = 1;
								    httpHeaderPropertyKeyValueIndex < httpHeaderPropertyKeyValue.Length;
								    httpHeaderPropertyKeyValueIndex++)
								{
									var httpHeaderPropertyKeyValueSection
									    = httpHeaderPropertyKeyValue[httpHeaderPropertyKeyValueIndex].Trim();
									if (httpHeaderPropertyKeyValueSection?.Length > 0)
									{
										if (httpHeaderPropertyValue == null)
										{
											httpHeaderPropertyValue = httpHeaderPropertyKeyValueSection;
										}
										else
										{
											httpHeaderPropertyValue += httpHeaderPropertyKeyValueSection;
										}
									}
								}
							}
						}
						if (httpHeaderPropertyKey?.Length > 0)
						{
							httpHeader ??= new Dictionary<string, StringValues>();
							// !!!
							httpHeader.Add(
							    httpHeaderPropertyKey,
							    httpHeaderPropertyValue);
							// !!!
						}
					}
				}
			}
			return httpHeader;
		}



		#endregion



		#endregion

		////////////////////////////////////////////////
		// @自身实现
		////////////////////////////////////////////////

		#region 自身实现

		public RequestInfo()
		{ }

		public RequestInfo(RequestInfo anotherRequestInfo)
		{
			this.RequestPath = anotherRequestInfo.RequestPath;
			this.RequestQueryString = anotherRequestInfo.RequestQueryString;
			this.RequestHttpHeader = anotherRequestInfo.RequestHttpHeader;
			this.RequestBodyBytes = anotherRequestInfo.RequestBodyBytes;
		}

		public RequestInfo(
		    string? requestPath,
		    string? requestQueryString,
		    Dictionary<string, StringValues>? requestHttpHeaders,
		    byte[]? requestBodyBytes)
		{
			this.RequestPath = requestPath;
			this.RequestQueryString = requestQueryString;
			this.RequestHttpHeader = requestHttpHeaders;
			this.RequestBodyBytes = requestBodyBytes;
		}

		#endregion
	}
}
