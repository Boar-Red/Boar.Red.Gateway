using Microsoft.AspNetCore.Mvc;

namespace Boar.Red.Gateway.Controllers;

public class IndexPageController : Controller
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	static byte[]? _lastIndexHtmlFileBytes;

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public IActionResult Index()
	{
		try
		{
			var indexHtmlFilePath = BaoXia.Utils.Environment.WebRootDirectoryPath + "index.html";
			// !!!
			var indexHtmlFileBytes = System.IO.File.ReadAllBytes(indexHtmlFilePath);
			// !!!
			if (indexHtmlFileBytes?.Length > 0)
			{
				_lastIndexHtmlFileBytes = indexHtmlFileBytes;
			}
		}
		catch
		{ }

		if (_lastIndexHtmlFileBytes?.Length > 0)
		{
			return new FileContentResult(
			    _lastIndexHtmlFileBytes,
			    "text/html; utf-8");
		}
		return new StatusCodeResult(404);
	}

	#endregion
}