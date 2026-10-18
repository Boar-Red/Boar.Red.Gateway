using Microsoft.AspNetCore.Mvc;

namespace Boar.Red.Gateway.Test.Utils.RouteRequestPaths;

public class ConfigController : Controller
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[HttpGet]
	public IActionResult GetSystemAdvancedSettings()
	{
		return Content("management controller");
	}

	[HttpPost]
	public IActionResult PostOnly()
	{
		return Content("management controller");
	}

	#endregion
}
