using Microsoft.AspNetCore.Mvc;

namespace Boar.Red.Gateway.Test.Endpoint.Controllers
{
	public class FileTestController : Controller
	{
		public IActionResult Index()
		{
			return View();
		}

		public IActionResult DownloadFileWithFileSizeInMB(int fileSizeInMB)
		{
			var fileBytes = new byte[fileSizeInMB * 1024 * 1024];
			return new FileStreamResult(new MemoryStream(fileBytes), "application/zip")
			{
				FileDownloadName = "testFileWithSize_" + fileSizeInMB + "MB.zip"
			};
		}
	}
}
