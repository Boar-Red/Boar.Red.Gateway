using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Globalization;

namespace Boar.Red.Gateway.Test.Endpoint.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class SseTestController : ControllerBase
{
	////////////////////////////////////////////////
	// @静态变量
	////////////////////////////////////////////////

	#region 静态变量

	private static readonly ConcurrentDictionary<string, bool> _connectionAbortStates = new();

	#endregion


	////////////////////////////////////////////////
	// @类方法
	////////////////////////////////////////////////

	#region 类方法

	public static void ResetConnectionAbortState(string connectionId)
	{
		_connectionAbortStates.TryRemove(connectionId, out _);
	}

	public static bool IsConnectionAborted(string connectionId)
	{
		return _connectionAbortStates.TryGetValue(connectionId, out var isAborted)
			&& isAborted;
	}

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[HttpGet]
	public IActionResult NoContentResponse()
	{
		return NoContent();
	}

	[HttpGet]
	public IActionResult InvalidContentType()
	{
		return Content("{\"message\":\"not SSE\"}", "application/json");
	}

	[HttpGet]
	public async Task Stream(
		string? connectionId = null,
		int initialDelayMilliseconds = 0,
		int eventIntervalMilliseconds = 0,
		int eventsCount = 1,
		int holdOpenMilliseconds = 0)
	{
		if (!string.IsNullOrEmpty(connectionId))
		{
			_connectionAbortStates[connectionId] = false;
		}

		Response.StatusCode = StatusCodes.Status200OK;
		Response.ContentType = "text/event-stream";
		Response.Headers.CacheControl = "no-cache, no-transform";
		Response.Headers.Append("X-Accel-Buffering", "no");
		await Response.StartAsync(HttpContext.RequestAborted);
		await Response.Body.FlushAsync(HttpContext.RequestAborted);

		try
		{
			if (initialDelayMilliseconds > 0)
			{
				await Task.Delay(
					initialDelayMilliseconds,
					HttpContext.RequestAborted);
			}

			var lastEventId = Request.Headers["Last-Event-ID"].ToString();
			for (var eventIndex = 1; eventIndex <= eventsCount; eventIndex++)
			{
				if (eventIndex > 1 && eventIntervalMilliseconds > 0)
				{
					await Task.Delay(
						eventIntervalMilliseconds,
						HttpContext.RequestAborted);
				}

				var eventContent =
					"id: " + eventIndex.ToString(CultureInfo.InvariantCulture) + "\n"
					+ "event: gateway-test\n"
					+ "data: sequence=" + eventIndex.ToString(CultureInfo.InvariantCulture)
					+ ";lastEventId=" + lastEventId + "\n\n";
				await Response.WriteAsync(
					eventContent,
					HttpContext.RequestAborted);
				await Response.Body.FlushAsync(HttpContext.RequestAborted);
			}

			if (holdOpenMilliseconds > 0)
			{
				await Task.Delay(
					holdOpenMilliseconds,
					HttpContext.RequestAborted);
			}
		}
		catch (OperationCanceledException)
			when (HttpContext.RequestAborted.IsCancellationRequested)
		{
			// 客户端关闭 SSE 连接属于正常结束。
		}
		finally
		{
			if (!string.IsNullOrEmpty(connectionId)
				&& HttpContext.RequestAborted.IsCancellationRequested)
			{
				_connectionAbortStates[connectionId] = true;
			}
		}
	}

	#endregion

}