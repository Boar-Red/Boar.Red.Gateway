using Boar.Red.Gateway.Constants;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;

namespace Boar.Red.Gateway
{
	public class ClientRequestPathInfo : IDisposable
	{
		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性

		public string? CatalogPath { get; set; }
		public string? RelativePath { get; set; }

		public string? RelativePathWithQuery
		{
			get
			{
				var relativePath = this.RelativePath;
				var queryString = this.QueryString;
				if (queryString?.Length > 0)
				{
					if (relativePath?.Length > 0)
					{
						if (!relativePath.EndsWith(PathKeywords.QuerySymbol))
						{
							relativePath += PathKeywords.QuerySymbol;
						}
						relativePath += queryString;
					}
					else
					{
						relativePath = queryString;
					}
				}
				return relativePath;
			}
		}

		public string? QueryString { get; set; }

		public List<string>? NamesOfEndPointExcepted { get; set; }

		public WebSocket? ClientWebSocketAccepted { get; set; }

		public bool IsValid
		{
			get
			{
				if (this.CatalogPath?.Length > 0
				    || this.RelativePath?.Length > 0)
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

		public ClientRequestPathInfo(
		    string? catalogPath,
		    string? relativePath,
		    string? queryString)
		{
			this.CatalogPath = catalogPath;
			this.RelativePath = relativePath;
			this.QueryString = queryString;
		}

		~ClientRequestPathInfo()
		{
			//!!!⚠ 不要在“业务，规则参数释放的时候”，释放相关的网络资源，传输还在进行中 ⚠!!!
			//var clientWebSocketAccepted = this.ClientWebSocketAccepted;
			//if (clientWebSocketAccepted != null)
			//{
			//        clientWebSocketAccepted?.Dispose();
			//        this.ClientWebSocketAccepted = null;
			//}
		}

		#endregion


		////////////////////////////////////////////////
		// @实现“IDisposable”
		////////////////////////////////////////////////

		#region 实现“IDisposable”

		public void Dispose()
		{
			//!!!⚠ 不要在“业务，规则参数释放的时候”，释放相关的网络资源，传输还在进行中 ⚠!!!
			//var clientWebSocketAccepted = this.ClientWebSocketAccepted;
			//if (clientWebSocketAccepted != null)
			//{
			//        clientWebSocketAccepted?.Dispose();
			//        this.ClientWebSocketAccepted = null;
			//}

			GC.SuppressFinalize(this);
		}


		#endregion


	}
}
