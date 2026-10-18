using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test
{
	[TestClass]
	public class PathMatchTest
	{

		////////////////////////////////////////////////
		// @静态变量
		////////////////////////////////////////////////

		#region 静态变量

		static HttpClient _httpClient = new();


		#endregion


		////////////////////////////////////////////////
		// @类方法
		////////////////////////////////////////////////

		#region 类方法

		static async Task<bool> VerifyResponseWithRelativePathAsync(
			string relativePath,
			string? xValue = null)
		{
			var relativePathOriginal = relativePath;

			try
			{
				// !!! 移除“Match_”头 !!!
				{
					relativePath = relativePath.Replace(
						"Match_",
						null,
						StringComparison.OrdinalIgnoreCase);
				}
				// !!! 移除“_Test”尾 !!!
				{
					relativePath = relativePath.Replace(
						"_Test",
						null,
						StringComparison.OrdinalIgnoreCase);
				}

				// !!! 替换“_”分隔符 !!!
				{
					relativePath = relativePath.Replace("_", "/");
				}

				// !!! 替换“x”随机变量 !!!
				{
					if (xValue == null)
					{
						xValue = new Random().Next(1000000).ToString();
					}
					relativePath = relativePath.Replace(
						"x",
						xValue);
				}

				// !!! 替换“odd”奇数变量 !!!
				{
					relativePath = relativePath.Replace(
						"odd",
						(2 * new Random().Next(500000) + 1).ToString());
				}

				// !!! 替换“even”偶数变量 !!!
				{
					relativePath = relativePath.Replace(
						"odd",
						(2 * new Random().Next(500000) + 0).ToString());
				}

				// !!! 增加路由目录 !!!
				{
					relativePath = relativePathOriginal + "/" + relativePath;
				}

				var testUrl = Constants.RouterService.TestRouterRootPath + relativePath;
				Debug.WriteLine("路径匹配测试“" + relativePathOriginal + "”开始，测试Url：", testUrl);

				var response = await _httpClient.GetStringAsync(testUrl);
				var validResponse = relativePathOriginal;
				if (response.Equals(validResponse) == true)
				{
					return true;
				}
			}
			catch (Exception exception)
			{
				Debug.WriteLine("路径匹配测试“" + relativePathOriginal + "”失败，程序异常。", exception);
				//
				return false;
			}
			return false;
		}

		#endregion


		////////////////////////////////////////////////
		// 1，1个模糊匹配：全部，开头，中部 ，结尾：
		////////////////////////////////////////////////

		// 1.1/4，模糊匹配：全部：
		[TestMethod]
		public async Task Match_x_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_x_Test"));
		}

		// 1.2/4，模糊匹配：开头：
		[TestMethod]
		public async Task Match_x_section_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_x_section_Test"));
		}

		// 1.3/4，模糊匹配：中部：
		[TestMethod]
		public async Task Match_section_x_section_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_section_x_section_Test"));
		}

		// 1.4/4，模糊匹配：结尾：
		[TestMethod]
		public async Task Match_section_x_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_section_x_Test"));
		}

		////////////////////////////////////////////////
		// 2，2个模糊匹配：
		// 开头，中部，
		// 开头，结尾，
		// 中部，结尾。
		////////////////////////////////////////////////

		// 2.1/3，模糊匹配：开头，中部：
		[TestMethod]
		public async Task Match_x_section_x_section_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_x_section_x_section_Test"));
		}

		// 2.2/3，模糊匹配：开头，结尾：
		[TestMethod]
		public async Task Match_x_section_x_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_x_section_x_Test"));
		}

		// 2.3/3，模糊匹配：中部，结尾：
		[TestMethod]
		public async Task Match_section_x_section_x_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_section_x_section_x_Test"));
		}

		////////////////////////////////////////////////
		// 3，3个模糊匹配：
		// 开头中部结尾，
		// 开头，中部，结尾，
		// 开头中部，结尾，
		// 开头，中部结尾，
		////////////////////////////////////////////////

		// 3.1/4，模糊匹配：开头中部结尾：
		[TestMethod]
		public async Task Match_x_x_x_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_x_x_x_Test"));
		}


		// 3.2/4，模糊匹配：开头，中部，结尾：
		[TestMethod]
		public async Task Match_x_section_x_section_x_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_x_section_x_section_x_Test"));
		}

		// 3.3/4，模糊匹配：开头中部，结尾：
		[TestMethod]
		public async Task Match_x_x_section_x_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_x_x_section_x_Test"));
		}

		// 3.4/4，模糊匹配：开头，中部结尾：
		[TestMethod]
		public async Task Match_x_section_x_x_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_x_section_x_x_Test"));
		}


		////////////////////////////////////////////////
		// 4，数学表达式匹配：全部，开头，中部 ，结尾：
		////////////////////////////////////////////////

		// 4.1/4，数学表达式匹配：全部，奇数。
		[TestMethod]
		public async Task Match_odd_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_odd_Test"));
		}

		// 4.1/4，数学表达式匹配：全部，偶数。
		[TestMethod]
		public async Task Match_even_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_even_Test"));
		}

		// 4.2/4，开头，奇数：
		[TestMethod]
		public async Task Match_odd_section_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_odd_section_Test",
				"odd_section"));
		}

		// 4.2/4，开头，偶数：
		[TestMethod]
		public async Task Match_even_section_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_even_section_Test"));
		}

		// 4.3/4，中部，奇数 ：
		[TestMethod]
		public async Task Match_section_odd_section_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_section_odd_section_Test"));
		}

		// 4.3/4，中部，偶数 ：
		[TestMethod]
		public async Task Match_section_even_section_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_section_even_section_Test"));
		}

		// 4.4/4，结尾，奇数：
		[TestMethod]
		public async Task Match_section_odd_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_section_odd_Test"));
		}

		// 4.4/4，结尾，偶数：
		[TestMethod]
		public async Task Match_section_even_Test()
		{
			Assert.IsTrue(await VerifyResponseWithRelativePathAsync(
				"Match_section_even_Test"));
		}
	}
}