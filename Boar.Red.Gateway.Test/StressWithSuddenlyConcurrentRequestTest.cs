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
	public class StressWithSuddenlyConcurrentRequestTest
	{
		[TestMethod]
		[Description("突发压力测试用例。")]
		public async Task Test()
		{
			var cancelTokenSource = new CancellationTokenSource();
			const double testDurationMinutes = 60.0;
			const int suddenlyConcurrentRequestTasksCount = 5;
			const double suddenlyConcurrentRequestIntervalSeconds = 5.0;
			const double suddenlyConcurrentRequestDurationSeconds = 3.0;
			double testResponseDurationSeconds = 0.05;
			const int testConcurrentTasksCount = 10;
			int testConcurrentTasksCountFinished = 0;
			const double testRequestIntervalSeconds = 0.001;
			string testApiUrl
				= RouterService.TestRouterRootPath + "stress_Test/stressWithConcurrentRequestTest/get?responseDurationSeconds="
				+ testResponseDurationSeconds;

			var httpClient = new HttpClient(new HttpClientHandler());
			var requestTasks = new List<Task>();

			var requestsCount = 0;
			var requestsCountSuccessed = 0;
			var requestsCountFailed = 0;
			var requestsCountFailedWithResponse_TooManyRequests = 0;

			var globalTestBeginTime = DateTime.Now;


			////////////////////////////////////////////////
			// 日常流量：
			////////////////////////////////////////////////
			for (var taskIndex = 0;
				taskIndex < testConcurrentTasksCount;
				taskIndex++)
			{
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
						}
					}

					// !!!
					Interlocked.Increment(ref testConcurrentTasksCountFinished);
					// !!!
				}, cancelTokenSource.Token));
			}

			////////////////////////////////////////////////
			// 突发流量：
			////////////////////////////////////////////////
			for (var taskIndex = 0;
				taskIndex < suddenlyConcurrentRequestTasksCount;
				taskIndex++)
			{
				requestTasks.Add(Task.Run(async () =>
				{
					var testSectionDurationSeconds
					= suddenlyConcurrentRequestDurationSeconds
					+ suddenlyConcurrentRequestIntervalSeconds;

					var testBeginTime = DateTime.Now;
					var testEndTime = testBeginTime.AddMinutes(testDurationMinutes);
					while (testSectionDurationSeconds > 0.0
						&& DateTime.Now <= testEndTime)
					{
						try
						{
							if (suddenlyConcurrentRequestDurationSeconds <= 0.0F)
							{
#pragma warning disable CS0162 // 检测到无法访问的代码
								break;
#pragma warning restore CS0162 // 检测到无法访问的代码
							}

							var testSeconds = (DateTime.Now - globalTestBeginTime).TotalSeconds;
							var testSectionSeconds = testSeconds % testSectionDurationSeconds;
							if (testSectionSeconds < suddenlyConcurrentRequestDurationSeconds)
							{
								continue;
							}

							// !!!!
							Interlocked.Increment(ref requestsCount);
							// !!!

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
