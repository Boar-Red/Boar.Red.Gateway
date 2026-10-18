using Microsoft.AspNetCore.Http;

namespace Boar.Red.Gateway.Extensions
{
	/// <summary>
	/// Http请求类扩展。
	/// </summary>
	public static class HttpRequestExtension
	{
		/// <summary>
		/// 获取指定请求对象的路由名称。
		/// </summary>
		/// <param name="request">指定的请求对象。</param>
		/// <returns>指定请求对象的路由名称。</returns>
		public static ClientRequestPathInfo? CreateClientRequestPathInfo(this HttpRequest request)
		{
			return request.CreateClientRequestPathInfo(RunTime.RouteRequestPaths);
		}

		public static ClientRequestPathInfo? CreateClientRequestPathInfo(this HttpRequest request, Utils.RouteRequestPathOptions options)
		{
			return RouteRule.CreateClientRequestPathInfoWithClientRequestPath(request.Path.Value, request.QueryString.Value, options);
		}
	}
}