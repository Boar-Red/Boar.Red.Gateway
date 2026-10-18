using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace Boar.Red.Gateway.Test.Endpoint.Controllers
{
	public class PathMatchTestController : Controller
	{
		////////////////////////////////////////////////
		// 1，1个模糊匹配：全部，开头，中部 ，结尾：
		////////////////////////////////////////////////

		// 1.1/4，模糊匹配：全部：
		public IActionResult Match_x_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 1.2/4，模糊匹配：开头：
		public IActionResult Match_x_section_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 1.3/4，模糊匹配：中部：
		public IActionResult Match_section_x_section_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 1.4/4，模糊匹配：结尾：
		public IActionResult Match_section_x_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		////////////////////////////////////////////////
		// 2，2个模糊匹配：
		// 开头，中部，
		// 开头，结尾，
		// 中部，结尾。
		////////////////////////////////////////////////

		// 2.1/3，模糊匹配：开头，中部：
		public IActionResult Match_x_section_x_section_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 2.2/3，模糊匹配：开头，结尾：
		public IActionResult Match_x_section_x_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 2.3/3，模糊匹配：中部，结尾：
		public IActionResult Match_section_x_section_x_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		////////////////////////////////////////////////
		// 3，3个模糊匹配：
		// 开头中部结尾，
		// 开头，中部，结尾，
		// 开头中部，结尾，
		// 开头，中部结尾，
		////////////////////////////////////////////////

		// 3.1/4，模糊匹配：开头中部结尾：
		public IActionResult Match_x_x_x_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}


		// 3.2/4，模糊匹配：开头，中部，结尾：
		public IActionResult Match_x_section_x_section_x_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 3.3/4，模糊匹配：开头中部，结尾：
		public IActionResult Match_x_x_section_x_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 3.4/4，模糊匹配：开头，中部结尾：
		public IActionResult Match_x_section_x_x_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}


		////////////////////////////////////////////////
		// 4，数学表达式匹配：全部，开头，中部 ，结尾：
		////////////////////////////////////////////////

		// 4.1/4，数学表达式匹配：全部，奇数。
		public IActionResult Match_odd_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 4.1/4，数学表达式匹配：全部，偶数。
		public IActionResult Match_even_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 4.2/4，开头，奇数：
		public IActionResult Match_odd_section_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 4.2/4，开头，偶数：
		public IActionResult Match_even_section_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 4.3/4，中部，奇数 ：
		public IActionResult Match_section_odd_section_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 4.3/4，中部，偶数 ：
		public IActionResult Match_section_even_section_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 4.4/4，结尾，奇数：
		public IActionResult Match_section_odd_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}

		// 4.4/4，结尾，偶数：
		public IActionResult Match_section_even_Test()
		{
			return new ContentResult()
			{
				ContentType = "text/plain",
				Content = MethodBase.GetCurrentMethod()?.Name
			};
		}
	}
}
