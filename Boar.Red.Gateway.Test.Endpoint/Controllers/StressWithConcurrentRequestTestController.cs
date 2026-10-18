using Microsoft.AspNetCore.Mvc;

namespace Boar.Red.Gateway.Test.Endpoint.Controllers;

public class StressWithConcurrentRequestTestController : Controller
{
	////////////////////////////////////////////////
	// @静态常量
	////////////////////////////////////////////////

	#region 静态常量

	public const double ExceptionResponseRate = 0.1;

	#endregion



	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[HttpGet]
	public async Task<IActionResult> Get([FromQuery] double responseDurationSeconds)
	{
		if (responseDurationSeconds > 0.0)
		{
			// !!!
			await Task.Delay((int)(responseDurationSeconds * 1000));
			// !!!
		}

		var isResponseException = false;
		//var randomAccuracy = 1000;
		//if (Random.Shared.Next(0, randomAccuracy) < randomAccuracy * ExceptionResponseRate)
		//{
		//	isResponseException = true;
		//}

		if (isResponseException)
		{
			return new ContentResult()
			{
				StatusCode = (int)System.Net.HttpStatusCode.InternalServerError,
				ContentType = "text/plain; charset=utf-8",
				Content = $"压力测试接口“stressTest/get”，响应正常（模拟异常）, 指定的响应时长为：{responseDurationSeconds,0:F3} 秒。"
			};
		}

		return new ContentResult()
		{
			StatusCode = (int)System.Net.HttpStatusCode.OK,
			ContentType = "text/plain; charset=utf-8",
			Content = $"压力测试接口“stressTest/get”，响应正常, 指定的响应时长为：{responseDurationSeconds,0:F3} 秒。"
		};
	}

	#endregion
}