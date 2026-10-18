using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;

namespace Boar.Red.Gateway.Extensions;

public static class RouteRequestPathOptionsFromEndpointRouteBuilderExtension
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public static RouteRequestPathOptions CreateRouteRequestPathOptions(this IEndpointRouteBuilder endpoints, string pathSpliter)
	{
		var actions = endpoints.ServiceProvider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>();
		return new RouteRequestPathOptions(pathSpliter, actions.Select(action => action.ControllerName));
	}

	public static void MapGatewayRouteRequests(this IEndpointRouteBuilder endpoints, RouteRequestPathOptions options)
	{
		async System.Threading.Tasks.Task routeRequest(HttpContext context) { await Router.RouteInContext(context, options); }
		object? policies = options.PathSpliter.Length == 0
			? new Dictionary<string, object> { [PathKeywords.SystemRouteValueKey_RouteRequestCatalogPath] = new RouteRequestCatalogConstraint(options) } : null;
		var namedPattern = RoutePatternFactory.Parse(options.NamedRouteTemplate, defaults: null, parameterPolicies: policies);
		endpoints.Map(namedPattern, routeRequest).Add(builder => ((RouteEndpointBuilder)builder).Order = int.MaxValue);
		if (options.UnnamedRouteTemplate is { } unnamedTemplate)
		{
			endpoints.Map(unnamedTemplate, routeRequest).Add(builder => ((RouteEndpointBuilder)builder).Order = int.MaxValue);
		}
	}

	#endregion
}
