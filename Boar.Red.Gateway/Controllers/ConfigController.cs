using BaoXia.Constants;
using BaoXia.Constants.Models;
using BaoXia.Utils.Extensions;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.LogFiles;
using Boar.Red.Gateway.Services;
using Boar.Red.Gateway.Utils;
using Boar.Red.Gateway.ViewModels;
using Boar.Red.Service.Attributes;
using Boar.Red.Service.Constants;
using Boar.Red.Service.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Controllers;

public class ConfigController(GatewayConfigService configService, GatewayRestartService restartService) : Controller
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	private IActionResult ExecuteConfigAction(Action action, string operationName, Response? response = null)
	{
		response ??= new Response();
		try
		{
			if (!ModelState.IsValid)
			{
				throw new ArgumentException("请求参数格式无效。");
			}
			action();
			var session = HttpContext.Items[HttpContextItemKeys.ServiceAdministrator.SessionObject] as ServiceAdministratorSession;
			Log.UserOperate.Logs(this, operationName + "成功，当前用户：" + session?.Account + "。");
		}
		catch (Exception exception) when (exception is ArgumentException or JsonException or DecoderFallbackException)
		{
			response.Error = Error.InvalidRequest;
			response.ErrorDescription = exception.Message;
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(this, operationName + "失败。", exception);
			response.Error = Error.OperationFailed;
			response.ErrorDescription = exception is InvalidOperationException ? exception.Message : "操作失败，请检查服务器异常日志。";
		}
		return Json(response);
	}

	private IActionResult GetConfigData<T>(Func<T> getData)
	{
		Response.Headers.CacheControl = "no-store";
		var response = new ConfigDataResponse<T>();
		try
		{
			response.Data = getData();
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(this, "查询网关配置失败。", exception);
			response.Error = Error.ProgramError;
		}
		return Json(response);
	}

	[HttpPost]
	[ServiceAdministratorApi]
	public IActionResult SaveSystemBasicSettings([FromBody] SystemBasicSettingsInfo? request)
	{
		var response = new ConfigDataResponse<SystemBasicSettingsInfo>();
		return ExecuteConfigAction(() => response.Data = configService.SaveSystemBasicSettings(request ?? throw new ArgumentException("系统基本配置不能为空。")), "保存系统基本配置", response);
	}

	[HttpPost]
	[ServiceAdministratorApi]
	[RequestSizeLimit(GatewayCertificateFileStorage.FileSizeMax + 64 * 1024)]
	public async Task<IActionResult> UploadCertificateFile([FromForm] IFormFile? file)
	{
		Response.Headers.CacheControl = "no-store";
		try
		{
			if (!ModelState.IsValid || file == null)
			{
				throw new ArgumentException("请您选择证书文件或密码文件。");
			}
			await using var stream = file.OpenReadStream();
			var uploadInfo = await configService.UploadCertificateFileAsync(stream, file.FileName, file.Length, HttpContext.RequestAborted);
			return Json(new ConfigDataResponse<CertificateFileUploadInfo> { Data = uploadInfo });
		}
		catch (Exception exception)
		{
			return ExecuteConfigAction(() => System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw(), "上传证书文件");
		}
	}

	[HttpPost]
	[ServiceAdministratorApi]
	public IActionResult SaveRouteRules([FromBody] SaveRouteRulesRequest? request)
	{
		return ExecuteConfigAction(() => configService.SaveRouteRules(request ?? throw new ArgumentException("路由规则不能为空。")), "保存全部路由规则");
	}

	[HttpPost]
	[ServiceAdministratorApi]
	public IActionResult SaveIpBlacklist([FromBody] SaveIpBlacklistRequest? request)
	{
		return ExecuteConfigAction(() => configService.SaveIpBlacklist(request ?? throw new ArgumentException("IP 黑名单不能为空。")), "保存全部 IP 黑名单");
	}

	[HttpPost]
	[ServiceAdministratorApi]
	public IActionResult SaveAdministrators([FromBody] SaveAdministratorsRequest? request)
	{
		return ExecuteConfigAction(() => configService.SaveAdministrators(request ?? throw new ArgumentException("管理员信息不能为空。")), "保存管理员信息");
	}

	[HttpPost]
	[ServiceAdministratorApi]
	public IActionResult SaveSystemAdvancedSettings([FromBody] SystemAdvancedSettingsInfo? request)
	{
		return ExecuteConfigAction(() => configService.SaveSystemAdvancedSettings(request ?? throw new ArgumentException("系统高级配置不能为空。")), "保存系统高级配置");
	}

	[HttpPost]
	[ServiceAdministratorApi]
	[RequestSizeLimit(5 * 1024 * 1024)]
	public async Task<IActionResult> UploadConfigFile([FromForm] IFormFile? file)
	{
		if (file == null || file.Length <= 0 || file.Length > 5 * 1024 * 1024 || !string.Equals(Path.GetExtension(file.FileName), ".json", StringComparison.OrdinalIgnoreCase))
		{
			return ExecuteConfigAction(() => throw new ArgumentException("请上传不超过 5 MB 的 JSON 配置文件。"), "上传配置文件");
		}
		try
		{
			using var reader = new StreamReader(file.OpenReadStream(), new UTF8Encoding(false, true), true);
			var json = await reader.ReadToEndAsync(HttpContext.RequestAborted);
			return ExecuteConfigAction(() => configService.UploadConfigFile(json), "上传配置文件");
		}
		catch (DecoderFallbackException)
		{
			return ExecuteConfigAction(() => throw new ArgumentException("配置文件的文本编码无效，请使用 UTF-8 编码。"), "上传配置文件");
		}
	}

	[HttpPost]
	[ServiceAdministratorApi]
	public IActionResult RestartGateway()
	{
		return ExecuteConfigAction(() => restartService.ScheduleRestart(Response), "安排重启网关程序");
	}

	[ServiceAdministratorApi]
	public IActionResult SaveEndPointsState([FromBody] SaveEndPointsStateRequest? request)
	{
		var response = new Response();
		try
		{
			if (request?.IsValid == true)
			{
				lock (Config.Router.SyncRoot)
				{
					////////////////////////////////////////////////
					// 1/3，更新配置：
					////////////////////////////////////////////////
					var routeRulesInConfig = Config.Router.RouteRules;
					if (routeRulesInConfig?.Length > 0)
					{
						var routerEndPointStateInfes = request.RouterEndPointStateInfes;
						var routerNames = routerEndPointStateInfes!.Keys;
						foreach (var routerName in routerNames)
						{
							var routerNameFinally = routerName;
							if (routerName.Length < 1
							    || Router.RouterName_All.EqualsIgnoreCase(routerName))
							{
								routerNameFinally = null;
							}
							if (routerEndPointStateInfes.TryGetValue(routerName, out var endPointStateInfes)
							    && endPointStateInfes?.Count > 0)
							{
								foreach (var routeRule in routeRulesInConfig)
								{
									if (routerNameFinally == null
									|| routerNameFinally.EqualsIgnoreCase(routeRule.Name))
									{
										foreach (var endPointStateInfo in endPointStateInfes)
										{
											var endPointName = endPointStateInfo.Name;
											if (endPointName?.Length > 0
											    && endPointName.EqualsIgnoreCase(Router.EndPointName_All) != true)
											{
												var endPoints = routeRule.EndPoints;
												if (endPoints?.Length > 0)
												{
													foreach (var endPoint in endPoints)
													{
														if (endPointName.EqualsIgnoreCase(endPoint.Name))
														{
															// !!!
															endPoint.IsConnectionEnable = endPointStateInfo.IsEnable;
															// !!!
														}
													}
												}
											}
										}
									}
								}
							}
						}
					}

					////////////////////////////////////////////////
					// 2/3，保存配置：
					////////////////////////////////////////////////
					{
						if (!Config.Router.Save(true, out _))
						{
							throw new InvalidOperationException("Config file could not be saved.");
						}
					}

				}
				////////////////////////////////////////////////
				// 3/3，记录日志：
				////////////////////////////////////////////////
				var session = this.HttpContext.Items[HttpContextItemKeys.ServiceAdministrator.SessionObject] as ServiceAdministratorSession;
				{
					Log.UserOperate.Logs(this, "更新配置成功，当前用户：" + session?.Account + "。");
				}
			}
			else
			{
				response.Error = Error.InvalidRequest;
			}
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(this, "执行失败，程序异常。", exception);
			//
			response.Error = Error.ProgramError;
		}
		return Json(response);
	}

	[ServiceAdministratorApi]
	public IActionResult SaveEndPointState(string endPointName, bool isEnable)
	{
		var endPointStateInfo = new EndPointStateInfo(
		    endPointName,
		    isEnable);
		var routerEndPointStateInfes = new Dictionary<string, ICollection<EndPointStateInfo>>();
		{
			routerEndPointStateInfes.Add(
			    Router.RouterName_All,
			    [
			    endPointStateInfo
			    ]);
		}

		var request = new SaveEndPointsStateRequest()
		{
			RouterEndPointStateInfes = routerEndPointStateInfes
		};
		return this.SaveEndPointsState(request);
	}

	[HttpGet]
	[ServiceAdministratorApi]
	public IActionResult GetSystemBasicSettings()
	{
		return GetConfigData(configService.GetSystemBasicSettings);
	}

	[HttpGet]
	[ServiceAdministratorApi]
	public IActionResult GetRouteRules()
	{
		return GetConfigData(configService.GetRouteRules);
	}

	[HttpGet]
	[ServiceAdministratorApi]
	public IActionResult GetIpBlacklist()
	{
		return GetConfigData(configService.GetIpBlacklist);
	}

	[HttpGet]
	[ServiceAdministratorApi]
	public IActionResult GetAdministrators()
	{
		return GetConfigData(configService.GetAdministrators);
	}

	[HttpGet]
	[ServiceAdministratorApi]
	public IActionResult GetSystemAdvancedSettings()
	{
		return GetConfigData(configService.GetSystemAdvancedSettings);
	}

	[HttpGet]
	[ServiceAdministratorApi]
	public IActionResult DownloadConfigFile()
	{
		try
		{
			Response.Headers.CacheControl = "no-store";
			return File(configService.DownloadConfigFile(), "application/json", "RouterConfig.json");
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(this, "下载配置文件失败。", exception);
			return Json(new Response { Error = Error.OperationFailed, ErrorDescription = "下载配置文件失败。" });
		}
	}

	#endregion
}
