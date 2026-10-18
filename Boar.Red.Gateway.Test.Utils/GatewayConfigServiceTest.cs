using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.Controllers;
using Boar.Red.Gateway.Defines.ViewModels;
using Boar.Red.Gateway.Services;
using Boar.Red.Gateway.Utils;
using Boar.Red.Gateway.ViewModels;
using Boar.Red.Service.Models;
using Boar.Red.Service.WebService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test.Utils;

[TestClass]
[DoNotParallelize]
public class GatewayConfigServiceTest
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	private string _directory = null!;
	private string _routerPath = null!;
	private RouterConfigForConfigApiTest _router = null!;
	private BlockRecordConfigForConfigApiTest _blockRecord = null!;
	private GatewayConfigService _service = null!;
	private GatewayCertificateFileStorage _certificateFiles = null!;

	#endregion

	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	private JsonObject ReadDocument()
	{
		return ConfigDocumentUtil.Parse(File.ReadAllText(_routerPath));
	}

	private static IpAddressBlockRecord Record(string ip)
	{
		return new IpAddressBlockRecord
		{
			TargetIpAddress = ip,
			BlockSourceName = "manual",
			BlockBeginTime = DateTimeOffset.Now.AddMinutes(-1),
			BlockEndTime = DateTimeOffset.Now.AddHours(1),
			BlockRemark = "封禁测试"
		};
	}

	[TestInitialize]
	public void Initialize()
	{
		_directory = Path.Combine(Path.GetTempPath(), "Boar.Red.Gateway.ConfigApiTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_directory);
		_routerPath = Path.Combine(_directory, "RouterConfig.json");
		_router = new RouterConfigForConfigApiTest
		{
			WebServiceConfig = new WebServiceConfig
			{
				HttpIPAddress = "0.0.0.0",
				HttpPort = 8080,
				HttpRequestBodySizeInMBMax = 16,
				WebSocketKeepAliveIntervalSeconds = 9,
				HttpsCertificateFileInfes = [new HttpsCertificateFileInfo { HostName = "example.test", CertificateFilePath = "example.pfx", CertificateFilePassword = "certificate-test-password" }]
			},
			ServiceAdministratorConfig = new ServiceAdministratorConfig
			{
				AdministratorIPAddresses = ["127.0.0.1"],
				SessionsCountMax = 7,
				Administrators = [new ServiceAdministrator { Account = "admin", Password = "test-password", EMail = "admin@example.test", ExpireTime = DateTime.Now.AddYears(10) }]
			},
			SMTPServerPassword = "smtp-test-password",
			RouteRules = [new RouteRule
			{
				Name = "api",
				ClientRequestPaths = ["/api/*"],
				EndPointRoutePolicy = EndPointRoutePolicy.Random,
				EndPointResponseForwardMode = EndPointResponseForwardMode.ServerSentEvents,
				EndPoints = [new EndPoint
				{
					Name = "primary",
					PathTemplate = "https://example.test/{RequestPath}",
					DefaultRouteRate = 0.75,
					RPSMax = 37,
					RouteRateControllInfes = [new RouteRateControllInfo { DurationSeconds = 3, RouteRate = 0.1 }]
				}]
			}]
		};
		var document = ConfigDocumentUtil.ToDocument(_router);
		document["customFutureOption"] = 42;
		File.WriteAllText(_routerPath, document.ToJsonString(ConfigDocumentUtil.JsonOptions), new UTF8Encoding(true));
		_router.FilePath = _routerPath;
		_router.DisableFileWatcher();
		_blockRecord = new BlockRecordConfigForConfigApiTest { FilePath = Path.Combine(_directory, "BlockRecordConfig.json") };
		_blockRecord.DisableFileWatcher();
		_certificateFiles = new GatewayCertificateFileStorage(_directory, Path.Combine(_directory, "temporary"));
		_service = new GatewayConfigService(_router, _blockRecord, _certificateFiles);
	}

	[TestCleanup]
	public void Cleanup()
	{
		_certificateFiles?.Dispose();
		_router?.Dispose();
		_blockRecord?.Dispose();
		var expectedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Boar.Red.Gateway.ConfigApiTests")) + Path.DirectorySeparatorChar;
		if (_directory != null && Path.GetFullPath(_directory).StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_directory))
		{
			Directory.Delete(_directory, true);
		}
	}

	[TestMethod]
	public void BasicSettings_ConvertUnitsPreserveHiddenSettingsAndPasswordsAndCreateBackup()
	{
		var info = _service.GetSystemBasicSettings();
		Assert.IsNull(info.SmtpPassword);
		Assert.IsTrue(info.HasSmtpPassword);
		Assert.AreEqual(1024, info.CompressionMinLength);
		info.HttpPort = 8081;
		info.RequestMaxSize = 32;
		info.CompressionMinLength = 1.5;
		info.CompressionAlgorithm = "Brotli";
		info.CacheStatusCodes = "200，201\n204";
		info.HttpOrigins = "https://example.test, https://second.test";
		_service.SaveSystemBasicSettings(info);
		Assert.AreEqual(8081, _router.WebServiceConfig!.HttpPort);
		Assert.AreEqual(32, _router.WebServiceConfig.HttpRequestBodySizeInMBMax);
		Assert.AreEqual(1536, _router.ClientResponseCompressionContentMinLength);
		Assert.AreEqual("br", _router.ClientResponseCompressionMethod);
		CollectionAssert.AreEqual(new[] { 200, 201, 204 }, _router.EndPointResponseCacheValidHttpStatusCodes!);
		Assert.AreEqual("smtp-test-password", _router.SMTPServerPassword);
		Assert.AreEqual("certificate-test-password", _router.WebServiceConfig.HttpsCertificateFileInfes![0].CertificateFilePassword);
		Assert.AreEqual(9, _router.WebServiceConfig.WebSocketKeepAliveIntervalSeconds);
		Assert.AreEqual(EndPointResponseForwardMode.ServerSentEvents, _router.RouteRules![0].EndPointResponseForwardMode);
		Assert.AreEqual(42, ReadDocument()["customFutureOption"]!.GetValue<int>());
		CollectionAssert.AreEqual(new byte[] { 239, 187, 191 }, File.ReadAllBytes(_routerPath).Take(3).ToArray());
		Assert.HasCount(1, Directory.GetFiles(_directory, "RouterConfig.json.Backup_*"));
	}

	[TestMethod]
	public void BasicSettings_InvalidValuesOrUninitializedPathDoNotChangeFileOrMemory()
	{
		var bytes = File.ReadAllBytes(_routerPath);
		var info = _service.GetSystemBasicSettings();
		info.HttpPort = 70000;
		Assert.Throws<ArgumentException>(() => _service.SaveSystemBasicSettings(info));
		CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_routerPath));
		Assert.AreEqual(8080, _router.WebServiceConfig!.HttpPort);
		info.HttpPort = 8082;
		_router.FilePath = null;
		Assert.Throws<InvalidOperationException>(() => _service.SaveSystemBasicSettings(info));
		Assert.AreEqual(8080, _router.WebServiceConfig.HttpPort);
	}

	[TestMethod]
	public async Task Certificates_UploadStaysTemporaryUntilSaveAndReturnsNormalizedPathsAsync()
	{
		var certificateBytes = new byte[] { 1, 2, 3, 4 };
		using var source = new MemoryStream(certificateBytes);
		var upload = await _service.UploadCertificateFileAsync(source, "../same-name.pfx", source.Length);
		Assert.AreEqual("same-name.pfx", upload.FileName);
		var temporaryPath = Path.Combine(_certificateFiles.TemporaryDirectory, upload.UploadToken!);
		Assert.IsTrue(File.Exists(temporaryPath));
		Assert.IsFalse(Directory.Exists(Path.Combine(_directory, "CertificateFiles")));
		Assert.AreEqual("example.pfx", _router.WebServiceConfig!.HttpsCertificateFileInfes![0].CertificateFilePath);
		using var passwordSource = new MemoryStream(Encoding.UTF8.GetBytes("your-password"));
		var passwordUpload = await _service.UploadCertificateFileAsync(passwordSource, "password.txt", passwordSource.Length);
		var info = _service.GetSystemBasicSettings();
		info.Certificates![0].CertificateFileName = upload.FileName;
		info.Certificates[0].CertificateUploadToken = upload.UploadToken;
		info.Certificates[0].PasswordFileName = passwordUpload.FileName;
		info.Certificates[0].PasswordUploadToken = passwordUpload.UploadToken;
		var saved = _service.SaveSystemBasicSettings(info);
		var certificate = saved.Certificates![0];
		Assert.StartsWith("CertificateFiles/", certificate.CertificateFileName!);
		Assert.StartsWith("CertificateFiles/", certificate.PasswordFileName!);
		Assert.AreEqual("same-name.pfx", Path.GetFileName(certificate.CertificateFileName));
		Assert.AreEqual("password.txt", Path.GetFileName(certificate.PasswordFileName));
		CollectionAssert.AreEqual(certificateBytes, File.ReadAllBytes(Path.Combine(_directory, certificate.CertificateFileName!)));
		Assert.AreEqual("your-password", File.ReadAllText(Path.Combine(_directory, certificate.PasswordFileName!)));
		Assert.IsFalse(File.Exists(temporaryPath));
		Assert.IsFalse(File.Exists(temporaryPath + ".name"));
		Assert.IsFalse(File.Exists(Path.Combine(_certificateFiles.TemporaryDirectory, passwordUpload.UploadToken!)));
		Assert.IsNull(certificate.CertificateUploadToken);
		Assert.IsNull(certificate.PasswordUploadToken);
		Assert.IsNull(_router.WebServiceConfig.HttpsCertificateFileInfes![0].CertificateFilePassword);
		var documentText = File.ReadAllText(_routerPath);
		Assert.IsFalse(documentText.Contains("UploadToken", StringComparison.OrdinalIgnoreCase));
		Assert.IsFalse(documentText.Contains(_certificateFiles.TemporaryDirectory, StringComparison.Ordinal));
		_service.SaveSystemBasicSettings(saved);
		Assert.HasCount(2, Directory.GetFiles(Path.Combine(_directory, "CertificateFiles"), "*", SearchOption.AllDirectories));
	}

	[TestMethod]
	public async Task Certificates_PreserveOriginalNamesAndIsolateSameNameUploadsAsync()
	{
		const string fileName = "网关证书 (测试).PFX";
		Directory.CreateDirectory(Path.Combine(_directory, "CertificateFiles"));
		var existingPath = Path.Combine(_directory, "CertificateFiles", fileName);
		File.WriteAllBytes(existingPath, [9]);
		using var first = new MemoryStream([1]);
		using var second = new MemoryStream([2]);
		var firstUpload = await _service.UploadCertificateFileAsync(first, "C:\\fakepath\\" + fileName, first.Length);
		var secondUpload = await _service.UploadCertificateFileAsync(second, fileName, second.Length);
		var info = _service.GetSystemBasicSettings();
		info.Certificates =
		[
			new SystemCertificateInfo { Identity = -1, CertificateUploadToken = firstUpload.UploadToken, CertificateFileName = "changed-name.pfx" },
			new SystemCertificateInfo { Identity = -2, CertificateUploadToken = secondUpload.UploadToken }
		];
		var saved = _service.SaveSystemBasicSettings(info);
		Assert.AreNotEqual(saved.Certificates![0].CertificateFileName, saved.Certificates[1].CertificateFileName);
		Assert.AreEqual(fileName, Path.GetFileName(saved.Certificates[0].CertificateFileName));
		Assert.AreEqual(fileName, Path.GetFileName(saved.Certificates[1].CertificateFileName));
		CollectionAssert.AreEqual(new byte[] { 1 }, File.ReadAllBytes(Path.Combine(_directory, saved.Certificates[0].CertificateFileName!)));
		CollectionAssert.AreEqual(new byte[] { 2 }, File.ReadAllBytes(Path.Combine(_directory, saved.Certificates[1].CertificateFileName!)));
		CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(existingPath));
		Assert.HasCount(0, Directory.GetFiles(_certificateFiles.TemporaryDirectory));
	}

	[TestMethod]
	public async Task Certificates_PreviousTemporaryUploadsWithoutNameMetadataRemainSavableAsync()
	{
		using var source = new MemoryStream([1]);
		var upload = await _service.UploadCertificateFileAsync(source, "previous.pfx", source.Length);
		File.Delete(Path.Combine(_certificateFiles.TemporaryDirectory, upload.UploadToken! + ".name"));
		var info = _service.GetSystemBasicSettings();
		info.Certificates![0].CertificateUploadToken = upload.UploadToken;
		info.Certificates[0].CertificateFileName = upload.FileName;
		var saved = _service.SaveSystemBasicSettings(info);
		Assert.AreEqual("previous.pfx", Path.GetFileName(saved.Certificates![0].CertificateFileName));
	}

	[TestMethod]
	public void Certificates_ExpiryRoundTripsAndOldClientsPreserveItWhileExplicitClearRemovesIt()
	{
		var expiry = new DateTimeOffset(2031, 5, 6, 13, 14, 15, TimeSpan.FromHours(8));
		var info = _service.GetSystemBasicSettings();
		Assert.IsNull(info.Certificates![0].CertificateFileExpireTime);
		Assert.IsFalse(ConfigDocumentUtil.ToDocument(info.Certificates[0]).ContainsKey(nameof(SystemCertificateInfo.CertificateFileExpireTime)));
		info.Certificates[0].CertificateFileExpireTime = expiry;
		var saved = _service.SaveSystemBasicSettings(info);
		Assert.AreEqual(expiry, saved.Certificates![0].CertificateFileExpireTime);
		Assert.AreEqual(expiry, _service.GetSystemBasicSettings().Certificates![0].CertificateFileExpireTime);
		Assert.AreEqual(expiry, _router.WebServiceConfig!.HttpsCertificateFileInfes![0].CertificateFileExpireTime);
		Assert.AreEqual(expiry.Offset, _router.WebServiceConfig.HttpsCertificateFileInfes[0].CertificateFileExpireTime.Offset);
		var olderClient = _service.GetSystemBasicSettings();
		olderClient.Certificates![0].CertificateFileExpireTime = null;
		_service.SaveSystemBasicSettings(olderClient);
		Assert.AreEqual(expiry, _router.WebServiceConfig.HttpsCertificateFileInfes[0].CertificateFileExpireTime);
		olderClient.Certificates[0].IsCertificateFileExpireTimeCleared = true;
		var cleared = _service.SaveSystemBasicSettings(olderClient);
		Assert.IsNull(cleared.Certificates![0].CertificateFileExpireTime);
		Assert.AreEqual(default, _router.WebServiceConfig.HttpsCertificateFileInfes[0].CertificateFileExpireTime);
		Assert.IsFalse(ConfigDocumentUtil.ToDocument(cleared.Certificates[0]).ContainsKey(nameof(SystemCertificateInfo.CertificateFileExpireTime)));
	}

	[TestMethod]
	public async Task Certificates_SaveFailureKeepsTemporaryFilesAndCleansCopiedFilesAsync()
	{
		using var source = new MemoryStream([1, 2, 3]);
		var upload = await _service.UploadCertificateFileAsync(source, "certificate.pfx", source.Length);
		var info = _service.GetSystemBasicSettings();
		info.Certificates![0].CertificateUploadToken = upload.UploadToken;
		var bytes = File.ReadAllBytes(_routerPath);
		info.HttpPort = 70000;
		Assert.Throws<ArgumentException>(() => _service.SaveSystemBasicSettings(info));
		CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_routerPath));
		Assert.IsTrue(File.Exists(Path.Combine(_certificateFiles.TemporaryDirectory, upload.UploadToken!)));
		info.HttpPort = 8081;
		_router.FilePath = null;
		Assert.Throws<InvalidOperationException>(() => _service.SaveSystemBasicSettings(info));
		Assert.HasCount(0, Directory.GetFiles(Path.Combine(_directory, "CertificateFiles"), "*", SearchOption.AllDirectories));
		Assert.HasCount(0, Directory.GetDirectories(Path.Combine(_directory, "CertificateFiles")));
		Assert.IsTrue(File.Exists(Path.Combine(_certificateFiles.TemporaryDirectory, upload.UploadToken!)));
		_router.FilePath = _routerPath;
		_router.DisableFileWatcher();
		_service.SaveSystemBasicSettings(info);
		Assert.HasCount(1, Directory.GetFiles(Path.Combine(_directory, "CertificateFiles"), "*", SearchOption.AllDirectories));
	}

	[TestMethod]
	public async Task Certificates_UnsafeOrWrongTypeTokensAndExpiredUploadsDoNotChangeConfigAsync()
	{
		using var source = new MemoryStream([1, 2, 3]);
		var upload = await _service.UploadCertificateFileAsync(source, "password.txt", source.Length);
		var info = _service.GetSystemBasicSettings();
		var originalBytes = File.ReadAllBytes(_routerPath);
		foreach (var token in new[] { "../certificate.pfx", upload.UploadToken!, new string('a', 32) + ".pfx" })
		{
			info.Certificates![0].CertificateUploadToken = token;
			Assert.Throws<ArgumentException>(() => _service.SaveSystemBasicSettings(info));
			CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(_routerPath));
		}
		var path = Path.Combine(_certificateFiles.TemporaryDirectory, upload.UploadToken!);
		File.SetLastWriteTimeUtc(path, DateTime.UtcNow - GatewayCertificateFileStorage.TemporaryFileLifetime - TimeSpan.FromMinutes(1));
		info.Certificates![0].CertificateUploadToken = null;
		info.Certificates[0].PasswordUploadToken = upload.UploadToken;
		Assert.Throws<ArgumentException>(() => _service.SaveSystemBasicSettings(info));
		_certificateFiles.CleanupExpiredFiles();
		Assert.IsFalse(File.Exists(path));
		Assert.IsFalse(File.Exists(path + ".name"));
		CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(_routerPath));
	}

	[TestMethod]
	public async Task Certificates_DeletingRowsPreservesSurvivorPasswordAndIgnoresRemovedUploadAsync()
	{
		var info = _service.GetSystemBasicSettings();
		info.Certificates = [info.Certificates![0], new SystemCertificateInfo { Identity = -1, Domain = "second.test", CertificateFileName = "second.pfx" }];
		var saved = _service.SaveSystemBasicSettings(info);
		using var source = new MemoryStream([1, 2, 3]);
		var upload = await _service.UploadCertificateFileAsync(source, "unused.pfx", source.Length);
		saved.Certificates![1].CertificateUploadToken = upload.UploadToken;
		saved.Certificates = [saved.Certificates[0]];
		saved = _service.SaveSystemBasicSettings(saved);
		Assert.HasCount(1, saved.Certificates!);
		Assert.AreEqual(1, saved.Certificates![0].Identity);
		Assert.AreEqual("certificate-test-password", _router.WebServiceConfig!.HttpsCertificateFileInfes![0].CertificateFilePassword);
		Assert.IsTrue(File.Exists(Path.Combine(_certificateFiles.TemporaryDirectory, upload.UploadToken!)));
		saved.Certificates = [];
		_service.SaveSystemBasicSettings(saved);
		Assert.HasCount(0, _router.WebServiceConfig.HttpsCertificateFileInfes!);
	}

	[TestMethod]
	public void Certificates_DeletingFirstRowRenumbersSurvivorWithoutChangingItsHiddenPassword()
	{
		var document = ReadDocument();
		var web = ConfigDocumentUtil.Section<WebServiceConfig>(document, nameof(RouterConfig.WebServiceConfig));
		ConfigDocumentUtil.Set(web, nameof(WebServiceConfig.HttpsCertificateFileInfes), new[]
		{
			new HttpsCertificateFileInfo { CertificateFilePath = "first.pfx", CertificateFilePassword = "first-test-password" },
			new HttpsCertificateFileInfo { CertificateFilePath = "second.pfx", CertificateFilePassword = "second-test-password" }
		});
		_service.UploadConfigFile(document.ToJsonString());
		var info = _service.GetSystemBasicSettings();
		info.Certificates = [info.Certificates![1]];
		var saved = _service.SaveSystemBasicSettings(info);
		Assert.AreEqual(1, saved.Certificates![0].Identity);
		Assert.AreEqual("second-test-password", _router.WebServiceConfig!.HttpsCertificateFileInfes![0].CertificateFilePassword);
		_service.SaveSystemBasicSettings(saved);
		Assert.AreEqual("second-test-password", _router.WebServiceConfig.HttpsCertificateFileInfes![0].CertificateFilePassword);
	}

	[TestMethod]
	public async Task Certificates_UploadRejectsInvalidFilesAndIncompleteContentAsync()
	{
		using var invalid = new MemoryStream([1, 2, 3]);
		await Assert.ThrowsAsync<ArgumentException>(() => _service.UploadCertificateFileAsync(invalid, "file.exe", invalid.Length));
		await Assert.ThrowsAsync<ArgumentException>(() => _service.UploadCertificateFileAsync(invalid, "empty.pfx", 0));
		await Assert.ThrowsAsync<ArgumentException>(() => _service.UploadCertificateFileAsync(invalid, "large.pfx", GatewayCertificateFileStorage.FileSizeMax + 1));
		await Assert.ThrowsAsync<ArgumentException>(() => _service.UploadCertificateFileAsync(invalid, "short.pfx", 4));
		Assert.HasCount(0, Directory.GetFiles(_certificateFiles.TemporaryDirectory));
		foreach (var invalidName in new[] { "wrong:name.pfx", "CON.pfx", "LPT1.txt", "wrong?.pfx", "certificate.pfx " })
		{
			invalid.Position = 0;
			await Assert.ThrowsAsync<ArgumentException>(() => _service.UploadCertificateFileAsync(invalid, invalidName, invalid.Length));
		}
		using var first = new MemoryStream([1]);
		using var second = new MemoryStream([2]);
		var firstUpload = await _service.UploadCertificateFileAsync(first, "same.PFX", first.Length);
		var secondUpload = await _service.UploadCertificateFileAsync(second, "same.PFX", second.Length);
		Assert.AreNotEqual(firstUpload.UploadToken, secondUpload.UploadToken);
	}

	[TestMethod]
	public async Task Certificates_ControllerReturnsUploadTokenAndSavedConfigurationAsync()
	{
		var controller = new ConfigController(_service, null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
		using var source = new MemoryStream([1, 2, 3]);
		var result = (JsonResult)await controller.UploadCertificateFile(new FormFile(source, 0, source.Length, "file", "certificate.pfx"));
		var uploadResponse = (ConfigDataResponse<CertificateFileUploadInfo>)result.Value!;
		Assert.IsTrue(string.IsNullOrEmpty(uploadResponse.Error));
		Assert.IsNotNull(typeof(ConfigController).GetMethod(nameof(ConfigController.UploadCertificateFile))!
			.GetCustomAttributes(typeof(Boar.Red.Service.Attributes.ServiceAdministratorApiAttribute), true).SingleOrDefault());
		Assert.IsNotNull(uploadResponse.Data?.UploadToken);
		Assert.AreEqual("no-store", controller.Response.Headers.CacheControl.ToString());
		var info = _service.GetSystemBasicSettings();
		info.Certificates![0].CertificateUploadToken = uploadResponse.Data.UploadToken;
		var saveResponse = (ConfigDataResponse<SystemBasicSettingsInfo>)((JsonResult)controller.SaveSystemBasicSettings(info)).Value!;
		Assert.IsTrue(string.IsNullOrEmpty(saveResponse.Error));
		Assert.StartsWith("CertificateFiles/", saveResponse.Data!.Certificates![0].CertificateFileName!);
	}

	[TestMethod]
	public void RouteRules_RenamePreservesUnexposedFieldsAndMapsStagesAndTraffic()
	{
		var rules = _service.GetRouteRules();
		Assert.AreEqual(75, rules[0].EndPoints![0].TrafficPercent);
		rules[0].Name = "renamed-api";
		rules[0].AllocationMode = "主从模式";
		rules[0].CacheMode = "返回缓存并自动更新";
		rules[0].StagedTrafficControl = "1-3秒，10%；3-6秒，50%";
		rules[0].EndPoints![0].TrafficPercent = 50;
		_service.SaveRouteRules(new SaveRouteRulesRequest { RouteRules = rules });
		var rule = _router.RouteRules![0];
		Assert.AreEqual("renamed-api", rule.Name);
		Assert.AreEqual(EndPointResponseForwardMode.ServerSentEvents, rule.EndPointResponseForwardMode);
		Assert.AreEqual(EndPointRoutePolicy.MasterSlave, rule.EndPointRoutePolicy);
		Assert.AreEqual(ResponseCacheMode.ReturnCache_AutoUpdateCache, rule.ClientResponseCacheMode);
		var endPoint = rule.EndPoints![0];
		Assert.AreEqual("primary", endPoint.Name);
		Assert.AreEqual(37, endPoint.RPSMax);
		Assert.AreEqual(0.5, endPoint.DefaultRouteRate);
		Assert.HasCount(3, endPoint.RouteRateControllInfes!);
		Assert.AreEqual(1, endPoint.RouteRateControllInfes![0].DurationSeconds);
		Assert.AreEqual(0.5, endPoint.RouteRateControllInfes[0].RouteRate);
		Assert.AreEqual(2, endPoint.RouteRateControllInfes[1].DurationSeconds);
		Assert.AreEqual(0.1, endPoint.RouteRateControllInfes[1].RouteRate);
	}

	[TestMethod]
	public void RouteRules_InvalidStageDoesNotPartiallySaveAndEmptyArrayClearsRules()
	{
		var bytes = File.ReadAllBytes(_routerPath);
		var rules = _service.GetRouteRules();
		rules[0].Name = "changed";
		rules[0].StagedTrafficControl = "0-3秒，10%；2-4秒，20%";
		Assert.Throws<ArgumentException>(() => _service.SaveRouteRules(new SaveRouteRulesRequest { RouteRules = rules }));
		CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_routerPath));
		Assert.AreEqual("api", _router.RouteRules![0].Name);
		_service.SaveRouteRules(new SaveRouteRulesRequest { RouteRules = [] });
		Assert.HasCount(0, _router.RouteRules!);
	}

	[TestMethod]
	public void Administrators_HidePasswordAndPreserveItWhenRenamingAndPersistDisplayName()
	{
		var administrators = _service.GetAdministrators();
		Assert.IsNull(administrators[0].Password);
		Assert.IsTrue(administrators[0].HasPassword);
		administrators[0].Name = "系统管理员";
		administrators[0].Account = "renamed-admin";
		_service.SaveAdministrators(new SaveAdministratorsRequest { Administrators = administrators });
		Assert.AreEqual("test-password", _router.ServiceAdministratorConfig!.Administrators![0].Password);
		Assert.AreEqual("admin@example.test", _router.ServiceAdministratorConfig.Administrators[0].EMail);
		Assert.AreEqual("系统管理员", _service.GetAdministrators()[0].Name);
		Assert.AreEqual(administrators[0].Identity, _service.GetAdministrators()[0].Identity);
		Assert.AreEqual(7, _router.ServiceAdministratorConfig.SessionsCountMax);
		CollectionAssert.AreEqual(new[] { "127.0.0.1" }, _router.ServiceAdministratorConfig.AdministratorIPAddresses!);
	}

	[TestMethod]
	public void Administrators_RejectNewBlankPasswordAndRemovalOfLastActiveAdministrator()
	{
		var bytes = File.ReadAllBytes(_routerPath);
		Assert.Throws<ArgumentException>(() => _service.SaveAdministrators(new SaveAdministratorsRequest { Administrators = [] }));
		Assert.Throws<ArgumentException>(() => _service.SaveAdministrators(new SaveAdministratorsRequest
		{
			Administrators = [new SystemAdministratorInfo { Account = "new", Name = "新管理员", ExpireTime = DateTime.Now.AddYears(1) }]
		}));
		CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_routerPath));
	}

	[TestMethod]
	public void Blacklist_ReplaceRebuildsIndexAndSaveThenClearIsDurable()
	{
		_blockRecord.AddBlockRecords([Record("192.0.2.1")]);
		_service.SaveIpBlacklist(new SaveIpBlacklistRequest { BlockRecords = [Record("192.0.2.2")] });
		Assert.IsFalse(_blockRecord.IsIpBlocked("192.0.2.1", DateTimeOffset.Now));
		Assert.IsTrue(_blockRecord.IsIpBlocked("192.0.2.2", DateTimeOffset.Now));
		var snapshot = _service.GetIpBlacklist();
		snapshot[0].TargetIpAddress = "192.0.2.3";
		Assert.AreEqual("192.0.2.2", _blockRecord.BlockRecords![0].TargetIpAddress);
		_service.SaveIpBlacklist(new SaveIpBlacklistRequest { BlockRecords = [] });
		Assert.IsTrue(_blockRecord.Load());
		Assert.HasCount(0, _blockRecord.BlockRecords!);
		Assert.IsFalse(_blockRecord.IsIpBlocked("192.0.2.2", DateTimeOffset.Now));
	}

	[TestMethod]
	public void Blacklist_InvalidOrDuplicateRecordsDoNotClearCurrentRecords()
	{
		_blockRecord.AddBlockRecords([Record("192.0.2.1")]);
		Assert.Throws<ArgumentException>(() => _service.SaveIpBlacklist(new SaveIpBlacklistRequest { BlockRecords = [Record("invalid")] }));
		Assert.Throws<ArgumentException>(() => _service.SaveIpBlacklist(new SaveIpBlacklistRequest { BlockRecords = [Record("192.0.2.2"), Record("192.0.2.2")] }));
		Assert.AreEqual("192.0.2.1", _blockRecord.BlockRecords![0].TargetIpAddress);
	}

	[TestMethod]
	public void AdvancedSettings_ParseWhitelistPreserveAdministratorsAndRejectInvalidAddress()
	{
		_service.SaveSystemAdvancedSettings(new SystemAdvancedSettingsInfo { AdministratorLoginIpWhitelist = "127.0.0.1，::1\n192.0.2.*" });
		CollectionAssert.AreEqual(new[] { "127.0.0.1", "::1", "192.0.2.*" }, _router.ServiceAdministratorConfig!.AdministratorIPAddresses!);
		Assert.AreEqual("test-password", _router.ServiceAdministratorConfig.Administrators![0].Password);
		Assert.Throws<ArgumentException>(() => _service.SaveSystemAdvancedSettings(new SystemAdvancedSettingsInfo { AdministratorLoginIpWhitelist = "invalid" }));
		_service.SaveSystemAdvancedSettings(new SystemAdvancedSettingsInfo { AdministratorLoginIpWhitelist = "" });
		Assert.HasCount(0, _router.ServiceAdministratorConfig.AdministratorIPAddresses!);
	}

	[TestMethod]
	public void AdvancedSettings_PrefixPersistsWithoutChangingRuntimeAndOldClientsPreserveIt()
	{
		var effectivePrefix = RunTime.RouteRequestPaths.PathSpliter;
		Assert.AreEqual("@", _service.GetSystemAdvancedSettings().RouteRequestPathSpliter);
		_service.SaveSystemAdvancedSettings(new SystemAdvancedSettingsInfo { RouteRequestPathSpliter = "~" });
		Assert.AreEqual("~", _router.RouteRequestPathSpliter);
		Assert.AreEqual(effectivePrefix, _service.GetSystemAdvancedSettings().EffectiveRouteRequestPathSpliter);
		_service.SaveSystemAdvancedSettings(new SystemAdvancedSettingsInfo { AdministratorLoginIpWhitelist = "127.0.0.1" });
		Assert.AreEqual("~", _router.RouteRequestPathSpliter);
		_service.SaveSystemAdvancedSettings(new SystemAdvancedSettingsInfo { RouteRequestPathSpliter = "" });
		Assert.AreEqual("", _router.RouteRequestPathSpliter);
		Assert.AreEqual("", ReadDocument()[nameof(RouterConfig.RouteRequestPathSpliter)]!.GetValue<string>());
	}

	[TestMethod]
	public void AdvancedSettings_InvalidPrefixDoesNotChangeFileOrWhitelist()
	{
		var bytes = File.ReadAllBytes(_routerPath);
		Assert.Throws<ArgumentException>(() => _service.SaveSystemAdvancedSettings(new SystemAdvancedSettingsInfo
		{
			RouteRequestPathSpliter = "/",
			AdministratorLoginIpWhitelist = "192.0.2.1"
		}));
		CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_routerPath));
		Assert.AreEqual("@", _router.RouteRequestPathSpliter);
		CollectionAssert.AreEqual(new[] { "127.0.0.1" }, _router.ServiceAdministratorConfig!.AdministratorIPAddresses!);
	}

	[TestMethod]
	public void Upload_ValidatesPrefixAndAcceptsEmptyAndLegacyMissingField()
	{
		var bytes = File.ReadAllBytes(_routerPath);
		foreach (var value in new string?[] { "/", "%40", " ", null })
		{
			var invalid = ReadDocument();
			ConfigDocumentUtil.Set(invalid, nameof(RouterConfig.RouteRequestPathSpliter), value);
			Assert.Throws<ArgumentException>(() => _service.UploadConfigFile(invalid.ToJsonString()));
			CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_routerPath));
		}
		var document = ReadDocument();
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.RouteRequestPathSpliter), "");
		_service.UploadConfigFile(document.ToJsonString());
		Assert.AreEqual("", _router.RouteRequestPathSpliter);
		document.Remove(nameof(RouterConfig.RouteRequestPathSpliter));
		_service.UploadConfigFile(document.ToJsonString());
		Assert.AreEqual("@", _router.RouteRequestPathSpliter);
	}

	[TestMethod]
	public void Upload_RejectsMalformedPartialConfigAndInvalidEnumsWithoutChangingFile()
	{
		var bytes = File.ReadAllBytes(_routerPath);
		Assert.Throws<System.Text.Json.JsonException>(() => _service.UploadConfigFile("{"));
		Assert.Throws<ArgumentException>(() => _service.UploadConfigFile("{\"settings\":{\"administratorLoginIpWhitelist\":\"127.0.0.1\"}}"));
		var document = ReadDocument();
		document[nameof(RouterConfig.RouteRules)]![0]![nameof(RouteRule.EndPointRoutePolicyName)] = "invalid-policy";
		Assert.Throws<ArgumentException>(() => _service.UploadConfigFile(document.ToJsonString()));
		CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_routerPath));
		Assert.AreEqual("api", _router.RouteRules![0].Name);
	}

	[TestMethod]
	public void UploadAndDownload_FullConfigRoundTripPersistsAndReloadsWithBackup()
	{
		CollectionAssert.AreEqual(File.ReadAllBytes(_routerPath), _service.DownloadConfigFile());
		var document = ReadDocument();
		document[nameof(RouterConfig.WebServiceConfig)]![nameof(WebServiceConfig.HttpPort)] = 9090;
		_service.UploadConfigFile(document.ToJsonString());
		Assert.AreEqual(9090, _router.WebServiceConfig!.HttpPort);
		Assert.AreEqual(42, ReadDocument()["customFutureOption"]!.GetValue<int>());
		CollectionAssert.AreEqual(File.ReadAllBytes(_routerPath), _service.DownloadConfigFile());
		Assert.HasCount(1, Directory.GetFiles(_directory, "RouterConfig.json.Backup_*"));
	}

	[TestMethod]
	public void BasicSettings_LegacyCertificateIsEditableAndRemovingAllCertificatesClearsFallback()
	{
		var document = ReadDocument();
		var web = (JsonObject)document[nameof(RouterConfig.WebServiceConfig)]!;
		ConfigDocumentUtil.Set(web, nameof(WebServiceConfig.HttpsCertificateFileInfes), Array.Empty<HttpsCertificateFileInfo>());
		ConfigDocumentUtil.Set(web, nameof(WebServiceConfig.HttpsCertificateFilePath), "legacy.pfx");
		ConfigDocumentUtil.Set(web, nameof(WebServiceConfig.HttpsCertificateFilePassword), "legacy-test-password");
		_service.UploadConfigFile(document.ToJsonString());
		var info = _service.GetSystemBasicSettings();
		Assert.HasCount(1, info.Certificates!);
		Assert.AreEqual("legacy.pfx", info.Certificates![0].CertificateFileName);
		_service.SaveSystemBasicSettings(info);
		Assert.AreEqual("legacy-test-password", _router.WebServiceConfig!.HttpsCertificateFileInfes![0].CertificateFilePassword);
		Assert.IsNull(_router.WebServiceConfig.HttpsCertificateFilePath);
		info.Certificates = [];
		_service.SaveSystemBasicSettings(info);
		Assert.HasCount(0, _router.WebServiceConfig.HttpsCertificateFileInfes!);
		Assert.IsNull(_router.WebServiceConfig.HttpsCertificateFilePath);
	}

	[TestMethod]
	public void Blacklist_OwnFileNotificationsDoNotDiscardNewRecordsAndExternalChangesStillReload()
	{
		_service.SaveIpBlacklist(new SaveIpBlacklistRequest { BlockRecords = [Record("192.0.2.1")] });
		_blockRecord.AddBlockRecords([Record("192.0.2.2")]);
		_blockRecord.NotifyFileChanged();
		Assert.HasCount(2, _blockRecord.BlockRecords!);
		Assert.IsTrue(_blockRecord.Save());
		_blockRecord.AddBlockRecords([Record("192.0.2.3")]);
		_blockRecord.NotifyFileChanged();
		Assert.HasCount(3, _blockRecord.BlockRecords!);
		var document = ConfigDocumentUtil.Parse(File.ReadAllText(_blockRecord.FinalFilePath!));
		ConfigDocumentUtil.Set(document, nameof(BlockRecordConfig.BlockRecords), new[] { Record("192.0.2.4") });
		File.WriteAllText(_blockRecord.FinalFilePath!, document.ToJsonString());
		File.SetLastWriteTimeUtc(_blockRecord.FinalFilePath!, DateTime.UtcNow.AddSeconds(2));
		_blockRecord.NotifyFileChanged();
		Assert.HasCount(1, _blockRecord.BlockRecords!);
		Assert.IsTrue(_blockRecord.IsIpBlocked("192.0.2.4", DateTimeOffset.Now));
	}

	[TestMethod]
	public void Save_UsesEnvironmentSpecificFile()
	{
		var environmentPath = Path.Combine(_directory, "RouterConfig.Test.json");
		File.Copy(_routerPath, environmentPath);
		var original = File.ReadAllBytes(_routerPath);
		_router.ConfigFileSecondFileExtensionNameSpecifed = "Test";
		var info = _service.GetSystemBasicSettings();
		info.HttpPort = 9191;
		_service.SaveSystemBasicSettings(info);
		CollectionAssert.AreEqual(original, File.ReadAllBytes(_routerPath));
		Assert.AreEqual(9191, ConfigDocumentUtil.Get<WebServiceConfig>(ConfigDocumentUtil.Parse(File.ReadAllText(environmentPath)), nameof(RouterConfig.WebServiceConfig))!.HttpPort);
	}

	#endregion
}
