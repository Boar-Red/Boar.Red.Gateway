using BaoXia.Constants;
using Boar.Red.Gateway.LogFiles;
using Boar.Red.Gateway.ViewModels;
using Boar.Red.Service.Attributes;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Controllers;

public class ResponseCacheController : Controller
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public IActionResult Index()
	{
		var response = new ResponseCacheIndexResponse();
		try
		{
			response.AllResponseCachesCount = Router.GetAllResponseCachesCount();
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(this, "获取响应缓存信息失败，程序异常。", exception);
			//
			response.Error = Error.ProgramError;
		}
		return Json(response);
	}

	[ServiceAdministratorApi]
	public async Task<IActionResult> Clear(string requestRelativePath)
	{
		var response = new ResponseCacheIndexResponse();
		try
		{
			var responseCachesCountRemoved = 0;
			if (requestRelativePath?.Length > 0)
			{
				responseCachesCountRemoved
				    = await Router.RemoveResponseCachesWithRequestRelativePathAsync(
				    requestRelativePath);
			}
			else
			{
				responseCachesCountRemoved
				    = Router.RemoveAllResponseCaches();
			}
			response.AllResponseCachesCount = Router.GetAllResponseCachesCount();
			response.ResponsesCountRemoved = responseCachesCountRemoved;
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(this, "清除响应缓存失败，程序异常。", exception);
			//
			response.Error = Error.ProgramError;
		}
		return Json(response);
	}

	#endregion
}
