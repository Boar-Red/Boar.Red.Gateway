using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Boar.Red.Gateway.Utils;

/// <summary>无 URL 前缀时，保留网关内部控制器的路径，包括方法或操作名无效的请求。</summary>
public sealed class RouteRequestCatalogConstraint(RouteRequestPathOptions options) : IRouteConstraint
{
	////////////////////////////////////////////////
	// @接口实现
	////////////////////////////////////////////////

	#region 接口实现

	public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
	{
		return values.TryGetValue(routeKey, out var value) && value is string catalogPath && !options.IsReservedCatalogPath(catalogPath);
	}

	#endregion
}
