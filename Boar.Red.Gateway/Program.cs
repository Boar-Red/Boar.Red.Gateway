using Boar.Red.Gateway;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Extensions;
using Boar.Red.Gateway.LogFiles;
using Boar.Red.Service.BrService.ViewModels;
using Boar.Red.Service.Constants;
using Boar.Red.Service.Extensions;
using Microsoft.AspNetCore.Builder;
using System.Reflection;

args = await Boar.Red.Gateway.Services.GatewayRestartService.WaitForParentExitAsync(args);

var appBuilder = WebApplication.CreateBuilder(args);


var httpServer = HttpServer.Kestrel;
#if WEBSERVER_IIS
	httpServer = HttpServer.IIS;
#endif
appBuilder.RunWithServerName(
#if DEBUG
	    "Development Server",
#else
	"Production Server",
#endif
    BaoXia.Utils.Environment.GetEnvironmentNameWith_ASPNETCORE_ENVIRONMENT(),
    Assembly.GetExecutingAssembly(),
    httpServer,
    //
    AESKeys.Default,
    //
    "ConfigFiles",
    "LogFiles",
    //
    Log.Logs,
    Log.Exception,
    Log.Warning,
    Log.BrService,
    //
    Config.Router,
    nameof(RouterConfig.WebServiceConfig),
    //
    RouteTemplateConstants.RouteTemplatesDefault,
    //
    new ServiceInitializationParam(
    //
    Config.Router,
    nameof(RouterConfig.WebServiceConfig),
    Config.Router,
    nameof(RouterConfig.BrServiceConfig),
    //
    true,
    false,
    false,
    new Boar.Red.Service.BrService.ServiceDiscoverer()),
    new ServiceAdministratorInitializationParam(
    Config.Router,
    nameof(RouterConfig.ServiceAdministratorConfig)),
    null,
    null,
    (app) =>
    {
	    var routeRequestPaths = app.CreateRouteRequestPathOptions(Config.Router.RouteRequestPathSpliter);
	    RunTime.InitializeRouteRequestPaths(routeRequestPaths);
	    app
	    .UseRouting()
	    .UseEndpoints(endPoints =>
	    {
		    endPoints.MapControllerRoute(name: "default", pattern: "{controller=indexPage}/{action=Index}/{id?}");
		    endPoints.MapGatewayRouteRequests(routeRequestPaths);
	    });
	    return app;
    },
    async (httpContext, next) =>
    {
	    if (httpContext.WebSockets.IsWebSocketRequest)
	    {
		    await Router.RouteInContext(httpContext);
	    }
	    else
	    {
		    // Call the next delegate/middleware in the pipeline
		    await next();
	    }
    });