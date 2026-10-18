using Boar.Red.Gateway.Test.Constants;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test
{
	[TestClass]
	public class StressWithSinCurveConcurrentRequestTest
	{
		[TestMethod]
		[Description("Sin曲线压力测试用例。")]
		public async Task Test()
		{
			var cancelTokenSource = new CancellationTokenSource();
			double testResponseDurationSeconds = 0.05;
			const double testDurationMinutes = 60.0;
			const double curveCycleSeconds = 10.0;
			const int testConcurrentTasksCount = 20;
			int testConcurrentTasksCountFinished = 0;
			const double testRequestIntervalSeconds = 0.001;
			string testApiUrl
				= RouterService.TestRouterRootPath + "stress_Test/stressWithConcurrentRequestTest/get?responseDurationSeconds="
				+ testResponseDurationSeconds;
			Random routeRateRandom = new();

			var httpClient = new HttpClient(new HttpClientHandler());
			var requestTasks = new List<Task>();

			var requestsCount = 0;
			var requestsCountSuccessed = 0;
			var requestsCountFailed = 0;
			var requestsCountFailedWithResponse_TooManyRequests = 0;

			for (var taskIndex = 0;
				taskIndex < testConcurrentTasksCount;
				taskIndex++)
			{
				var taskId = taskIndex + 1;
				requestTasks.Add(Task.Run(async () =>
				{
					var testBeginTime = DateTime.Now;
					var testEndTime = testBeginTime.AddMinutes(testDurationMinutes);
					while (DateTime.Now <= testEndTime)
					{
						try
						{
							// !!!!
							Interlocked.Increment(ref requestsCount);
							// !!!

							var testSeconds = (DateTime.Now - testBeginTime).TotalSeconds;

							var curveProgress = (testSeconds % curveCycleSeconds) / curveCycleSeconds;
							var angle = 180.0 * curveProgress;
							var rpsRate = Math.Sin(angle * Math.PI / 180.0);
							var taskIdRate = (double)(taskId) / (double)testConcurrentTasksCount;
							/*
                                                        var routeIndexNumber = routeRateRandom.NextDouble();
                                                        if (taskId == 1)
                                                        {
                                                                System.Diagnostics.Debug.WriteLine(
                                                                        $"任务{taskId}的执行进度为：" + (100.0 * curveProgress).ToString("F2") + "% ，"
                                                                        + $"角度为：{angle.ToString("F2")}，"
                                                                        + $"请求比例为：{(100.0 * rpsRate).ToString("F2")} % ，"
                                                                        + $"路由序号为：{(100.0 * routeIndexNumber).ToString("F2")}。");
                                                        }
                                                        */

							if (taskIdRate < rpsRate)
							{
								var responseString = await httpClient.GetStringAsync(testApiUrl, cancelTokenSource.Token);
								if (responseString?.Contains("响应正常") == true)
								{

									// !!!!
									Interlocked.Increment(ref requestsCountSuccessed);
									// !!!
								}
								else
								{
									Assert.Fail("请求失败，响应内容不正确。");
									// !!!!
									Interlocked.Increment(ref requestsCountFailed);
									// !!!
								}

								////////////////////////////////////////////////
								if (testRequestIntervalSeconds > 0.0)
								{
									await Task.Delay((int)(testRequestIntervalSeconds * 1000), cancelTokenSource.Token);
								}
								////////////////////////////////////////////////
							}
						}
						catch (Exception exception)
						{
							System.Diagnostics.Debug.WriteLine("请求失败，程序异常：", exception.ToString());

							// !!!!
							Interlocked.Increment(ref requestsCountFailed);
							// !!!
							if (exception is HttpRequestException httpRequestException
							&& httpRequestException.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
							{
								// !!!!
								Interlocked.Increment(ref requestsCountFailedWithResponse_TooManyRequests);
								// !!!
							}

							//
							throw;
							//
						}
					}

					// !!!
					Interlocked.Increment(ref testConcurrentTasksCountFinished);
					// !!!
				}, cancelTokenSource.Token));
			}

			// 监控线程：
			requestTasks.Add(Task.Run(async () =>
			{
				var testBeginTime = DateTime.Now;
				static void logTestInfoAction()
				{
					//System.Diagnostics.Debug.WriteLine(
					//        "第 " + (DateTime.Now - testBeginTime).TotalSeconds.ToString("F1") + " 秒，"
					//        + "已发送 " + requestsCount + " 个请求，"
					//        + requestsCountSuccessed + "个请求成功，"
					//        + requestsCountFailed + "个请求失败，"
					//        + "测试任务队列共有 " + testConcurrentTasksCount + " 个，"
					//        + "已完成 " + testConcurrentTasksCountFinished + " 个。");
				}

				do
				{
					// !!!
					logTestInfoAction();
					// !!!
					await Task.Delay(500, cancelTokenSource.Token);
					//
				} while (testConcurrentTasksCountFinished < testConcurrentTasksCount);
				// !!!
				logTestInfoAction();
				// !!!
			}, cancelTokenSource.Token));



			////////////////////////////////////////////////
			// !!!
			await Task.WhenAll([.. requestTasks]);
			// !!!
			////////////////////////////////////////////////


			if (requestsCountFailed > 0)
			{
				Assert.AreEqual(requestsCountFailedWithResponse_TooManyRequests, requestsCountFailed);
			}
			else
			{
				Assert.AreEqual(testConcurrentTasksCount, testConcurrentTasksCountFinished);
			}
		}
	}
}
