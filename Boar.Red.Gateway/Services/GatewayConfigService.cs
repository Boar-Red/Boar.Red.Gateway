using BaoXia.Utils;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Defines.ViewModels;
using Boar.Red.Gateway.Extensions.SystemSettings;
using Boar.Red.Gateway.Utils;
using Boar.Red.Gateway.ViewModels;
using Boar.Red.Service.Attributes;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Services;

[Service]
public class GatewayConfigService
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	private readonly RouterConfig _routerConfig;
	private readonly BlockRecordConfig _blockRecordConfig;
	private readonly GatewayCertificateFileStorage _certificateFiles;

	#endregion

	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	private JsonObject ReadRouterDocument()
	{
		var path = _routerConfig.FinalFilePath;
		return !string.IsNullOrEmpty(path) && File.Exists(path)
			? ConfigDocumentUtil.Parse(File.ReadAllText(path)) : ConfigDocumentUtil.ToDocument(_routerConfig);
	}

	private static void SaveDocument(ConfigFile config, JsonObject document)
	{
		var path = config.FinalFilePath;
		if (string.IsNullOrEmpty(path))
		{
			throw new InvalidOperationException("配置文件路径未初始化，无法保存配置。");
		}
		path = Path.GetFullPath(path);
		var exists = File.Exists(path);
		var encoding = new UTF8Encoding(false, true) as Encoding;
		var hasPreamble = false;
		if (exists)
		{
			using var reader = new StreamReader(path, encoding, true);
			reader.Peek();
			encoding = reader.CurrentEncoding;
			var preamble = encoding.GetPreamble();
			using var stream = File.OpenRead(path);
			var prefix = new byte[preamble.Length];
			hasPreamble = preamble.Length > 0 && stream.Read(prefix) == prefix.Length && prefix.SequenceEqual(preamble);
		}
		var content = encoding.GetBytes(document.ToJsonString(ConfigDocumentUtil.JsonOptions));
		var bytes = hasPreamble ? encoding.GetPreamble().Concat(content).ToArray() : content;
		var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		var backupPath = exists ? path + ".Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N") : null;
		var replaced = false;
		try
		{
			File.WriteAllBytes(tempPath, bytes);
			if (exists)
			{
				File.Replace(tempPath, path, backupPath);
			}
			else
			{
				File.Move(tempPath, path);
			}
			replaced = true;
			if (!config.Load())
			{
				throw new InvalidOperationException("保存后的配置文件无法重载。");
			}
			if (config is BlockRecordConfig blockRecordConfig)
			{
				blockRecordConfig.DidSaveConfigFileToPath(path);
			}
		}
		catch
		{
			if (replaced && backupPath != null)
			{
				File.Copy(backupPath, path, true);
				config.Load();
			}
			else if (replaced)
			{
				File.Delete(path);
			}
			throw;
		}
		finally
		{
			if (File.Exists(tempPath))
			{
				File.Delete(tempPath);
			}
		}
	}

	private void UpdateRouterDocument(Func<JsonObject, JsonObject> update)
	{
		lock (_routerConfig.SyncRoot)
		{
			var document = update(ReadRouterDocument());
			SaveDocument(_routerConfig, document);
		}
	}

	public GatewayConfigService() : this(Config.Router, Config.BlockRecord)
	{ }

	public GatewayConfigService(RouterConfig routerConfig, BlockRecordConfig blockRecordConfig, GatewayCertificateFileStorage? certificateFiles = null)
	{
		_routerConfig = routerConfig;
		_blockRecordConfig = blockRecordConfig;
		_certificateFiles = certificateFiles ?? GatewayCertificateFileStorage.Default;
	}

	public SystemBasicSettingsInfo SaveSystemBasicSettings(SystemBasicSettingsInfo info)
	{
		lock (_routerConfig.SyncRoot)
		{
			var document = info.ToRouterConfigDocument(ReadRouterDocument());
			_certificateFiles.SaveConfigurationFiles(info.Certificates, document, documentToSave => SaveDocument(_routerConfig, documentToSave));
			return _routerConfig.ToSystemBasicSettingsInfo();
		}
	}

	public Task<CertificateFileUploadInfo> UploadCertificateFileAsync(Stream source, string fileName, long length, CancellationToken cancellationToken = default)
	{
		return _certificateFiles.UploadAsync(source, fileName, length, cancellationToken);
	}

	public void SaveRouteRules(SaveRouteRulesRequest request)
	{
		UpdateRouterDocument(request.ToRouterConfigDocument);
	}

	public void SaveIpBlacklist(SaveIpBlacklistRequest request)
	{
		var records = GatewayConfigValidationUtil.ToValidatedBlockRecords(request.BlockRecords);
		lock (_blockRecordConfig.SyncRoot)
		{
			var document = ConfigDocumentUtil.ToDocument(_blockRecordConfig);
			ConfigDocumentUtil.Set(document, nameof(BlockRecordConfig.BlockRecords), records);
			ConfigDocumentUtil.Set(document, nameof(BlockRecordConfig.BlockRecordsUpdateTime), DateTimeOffset.Now);
			SaveDocument(_blockRecordConfig, document);
		}
	}

	public void SaveAdministrators(SaveAdministratorsRequest request)
	{
		UpdateRouterDocument(request.ToRouterConfigDocument);
	}

	public void SaveSystemAdvancedSettings(SystemAdvancedSettingsInfo info)
	{
		UpdateRouterDocument(info.ToRouterConfigDocument);
	}

	public void UploadConfigFile(string json)
	{
		var document = ConfigDocumentUtil.Parse(json);
		GatewayConfigValidationUtil.ValidateRouterDocument(document);
		lock (_routerConfig.SyncRoot)
		{
			SaveDocument(_routerConfig, document);
		}
	}

	public SystemBasicSettingsInfo GetSystemBasicSettings()
	{
		lock (_routerConfig.SyncRoot)
		{
			return _routerConfig.ToSystemBasicSettingsInfo();
		}
	}

	public SystemRouteRuleInfo[] GetRouteRules()
	{
		lock (_routerConfig.SyncRoot)
		{
			return (_routerConfig.RouteRules ?? []).Select((rule, index) => rule.ToSystemRouteRuleInfo(index + 1)).ToArray();
		}
	}

	public IpAddressBlockRecord[] GetIpBlacklist()
	{
		lock (_blockRecordConfig.SyncRoot)
		{
			return (_blockRecordConfig.BlockRecords ?? []).Select(record => new IpAddressBlockRecord
			{
				TargetIpAddress = record.TargetIpAddress,
				BlockSourceName = record.BlockSourceName,
				BlockBeginTime = record.BlockBeginTime,
				BlockEndTime = record.BlockEndTime,
				BlockRemark = record.BlockRemark
			}).ToArray();
		}
	}

	public SystemAdministratorInfo[] GetAdministrators()
	{
		lock (_routerConfig.SyncRoot)
		{
			return _routerConfig.ToSystemAdministratorInfes();
		}
	}

	public SystemAdvancedSettingsInfo GetSystemAdvancedSettings()
	{
		lock (_routerConfig.SyncRoot)
		{
			return _routerConfig.ToSystemAdvancedSettingsInfo();
		}
	}

	public byte[] DownloadConfigFile()
	{
		lock (_routerConfig.SyncRoot)
		{
			var path = _routerConfig.FinalFilePath;
			return !string.IsNullOrEmpty(path) && File.Exists(path)
				? File.ReadAllBytes(path) : Encoding.UTF8.GetBytes(ReadRouterDocument().ToJsonString(ConfigDocumentUtil.JsonOptions));
		}
	}

	#endregion
}
