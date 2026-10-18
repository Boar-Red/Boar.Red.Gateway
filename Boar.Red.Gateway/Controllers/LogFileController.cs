using BaoXia.Constants;
using BaoXia.Constants.Models;
using BaoXia.Utils;
using Microsoft.AspNetCore.Mvc;
using System;

namespace Boar.Red.Gateway.Controllers;

public class LogFileController : Controller
{
	[HttpGet]
	public IActionResult Index()
	{
		var isFlushLogBufferTaskCancelledCaption
		    = LogFile.IsFlushLogBufferTaskCancelled == true
		    ? "日志清理任务，已被取消"
		    : "日志清理任务，未被取消";
		var lastFlushLogBufferBeginTimeCaption
		    = LogFile.LastFlushLogBufferBeginTime.ToString("HH:mm:ss MM/dd yyyy");
		var lastFlushLogBufferEndTimeCaption
		    = LogFile.LastFlushLogBufferEndTime.ToString("HH:mm:ss MM/dd yyyy");
		var lastFlushLogBufferExceptionTimeCaption
		    = LogFile.LastFlushLogBufferExceptionTime.ToString("HH:mm:ss MM/dd yyyy");

		var logFilesStateDescription
		    = "当前日志文件状态："
		    + "\r\n当前缓存中日志条目数量：" + LogFile.GetAllLogRecordsCountInLogBuffer()
		    + "\r\n日志文件写入任务状态为：" + isFlushLogBufferTaskCancelledCaption
		    + "\r\n上一次日志文件写入开始时间为：" + lastFlushLogBufferBeginTimeCaption
		    + "\r\n上一次日志文件写入结束时间为：" + lastFlushLogBufferEndTimeCaption
		    + "\r\n上一次日志文件写入异常时间为：" + lastFlushLogBufferExceptionTimeCaption;

		return new ContentResult()
		{
			ContentType = "text/plain; charset=utf-8",
			Content = logFilesStateDescription
		};
	}

	[HttpGet]
	public IActionResult ClearAllLogBuffer()
	{

		var response = new Response();
		try
		{
			LogFile.ClearAllLogBuffer();
		}
		catch (Exception)
		{
			response.Error = Error.ProgramError;
		}
		return Json(response);
	}
}
