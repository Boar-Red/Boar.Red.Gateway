using Boar.Red.Gateway.Test.Constants;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test;

public class Program
{
	public static async Task Main(string[] _)
	{
		////////////////////////////////////////////////
		// 测试基础配置：
		////////////////////////////////////////////////

		// 开启的测试类型：
		var testTypesEnabled = new TestType[]
		{
			TestType.StressWithConcurrentRequestTest,
			//TestType.StressWithSuddenlyCurveConcurrentRequestTest,
			//TestType.StressWithSinCurveConcurrentRequestTest,
			//TestType.PathMatchTest
		};

		////////////////////////////////////////////////
		// 等待 10 秒，Router和Test.EndPoint 服务启动后，再开始测试：
		////////////////////////////////////////////////
		{
			Thread.Sleep(10000);
		}

		////////////////////////////////////////////////
		// 执行压力测试：
		////////////////////////////////////////////////
		if (testTypesEnabled.Contains(TestType.StressWithConcurrentRequestTest))
		{
			var stressWithConcurrentRequestTest = new StressWithConcurrentRequestTest();
			{
				// GET请求测试：
				await stressWithConcurrentRequestTest.Test();
			}
		}

		////////////////////////////////////////////////
		// 执行突发压力测试：
		////////////////////////////////////////////////
		if (
			testTypesEnabled.Contains(
				TestType.StressWithSuddenlyCurveConcurrentRequestTest
			)
		)
		{
			var stressWithCurveConcurrentRequestTest =
				new StressWithSuddenlyConcurrentRequestTest();
			{
				// GET请求测试：
				await stressWithCurveConcurrentRequestTest.Test();
			}
		}

		////////////////////////////////////////////////
		// 执行Sin曲线压力测试：
		////////////////////////////////////////////////
		if (testTypesEnabled.Contains(TestType.StressWithSinCurveConcurrentRequestTest))
		{
			var stressWithCurveConcurrentRequestTest =
				new StressWithSinCurveConcurrentRequestTest();
			{
				// GET请求测试：
				await stressWithCurveConcurrentRequestTest.Test();
			}
		}

		////////////////////////////////////////////////
		// 执行路径匹配测试：
		////////////////////////////////////////////////
		if (testTypesEnabled.Contains(TestType.PathMatchTest))
		{
			var pathMatchTest = new PathMatchTest();
			{
				////////////////////////////////////////////////
				// 1，1个模糊匹配：全部，开头，中部 ，结尾：
				////////////////////////////////////////////////
				// 1.1/4，模糊匹配：全部：
				pathMatchTest.Match_x_Test().Wait();

				// 1.2/4，模糊匹配：开头：
				pathMatchTest.Match_x_section_Test().Wait();

				// 1.3/4，模糊匹配：中部：
				pathMatchTest.Match_section_x_section_Test().Wait();

				// 1.4/4，模糊匹配：结尾：
				pathMatchTest.Match_section_x_Test().Wait();

				////////////////////////////////////////////////
				// 2，2个模糊匹配：// 开头，中部，// 开头，结尾，// 中部，结尾。
				////////////////////////////////////////////////

				// 2.1/3，模糊匹配：开头，中部：
				pathMatchTest.Match_x_section_x_section_Test().Wait();

				// 2.2/3，模糊匹配：开头，结尾：
				pathMatchTest.Match_x_section_x_Test().Wait();

				// 2.3/3，模糊匹配：中部，结尾：
				pathMatchTest.Match_section_x_section_x_Test().Wait();

				////////////////////////////////////////////////
				// 3，3个模糊匹配：
				// 开头中部结尾，
				// 开头，中部，结尾，
				// 开头中部，结尾，
				// 开头，中部结尾。
				////////////////////////////////////////////////
				// 3.1/4，模糊匹配：开头中部结尾：
				pathMatchTest.Match_x_x_x_Test().Wait();

				// 3.2/4，模糊匹配：开头，中部，结尾：
				pathMatchTest.Match_x_section_x_section_x_Test().Wait();

				// 3.3/4，模糊匹配：开头中部，结尾：
				pathMatchTest.Match_x_x_section_x_Test().Wait();

				// 3.4/4，模糊匹配：开头，中部结尾：
				pathMatchTest.Match_x_section_x_x_Test().Wait();

				////////////////////////////////////////////////
				// 4，数学表达式匹配：全部，开头，中部 ，结尾：
				////////////////////////////////////////////////

				// 4.1/4，数学表达式匹配：全部，奇数。
				pathMatchTest.Match_odd_Test().Wait();

				// 4.1/4，数学表达式匹配：全部，偶数。
				pathMatchTest.Match_even_Test().Wait();

				// 4.2/4，开头，奇数：
				pathMatchTest.Match_odd_section_Test().Wait();

				// 4.2/4，开头，偶数：
				pathMatchTest.Match_even_section_Test().Wait();

				// 4.3/4，中部，奇数 ：
				pathMatchTest.Match_section_odd_section_Test().Wait();

				// 4.3/4，中部，偶数 ：
				pathMatchTest.Match_section_even_section_Test().Wait();

				// 4.4/4，结尾，奇数：
				pathMatchTest.Match_section_odd_Test().Wait();

				// 4.4/4，结尾，偶数：
				pathMatchTest.Match_section_even_Test().Wait();
			}
		}

		////////////////////////////////////////////////
		// !!! 测试成功 !!!
		////////////////////////////////////////////////
		{
			System.Diagnostics.Debug.WriteLine("路由测试全部成功！");
			System.Console.Beep();
		}
	}
}
