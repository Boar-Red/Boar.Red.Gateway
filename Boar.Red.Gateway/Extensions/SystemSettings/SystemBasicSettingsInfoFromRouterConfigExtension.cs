using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Utils;
using Boar.Red.Gateway.ViewModels;
using Boar.Red.Service.WebService.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Boar.Red.Gateway.Extensions.SystemSettings;

public static class SystemBasicSettingsInfoFromRouterConfigExtension
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	private static string ToCompressionAlgorithm(string? method)
	{
		return method?.ToLowerInvariant() switch
		{
			"br" => "Brotli",
			"gzip" => "Gzip",
			"deflate" => "Deflate",
			"none" => "none",
			_ => "auto"
		};
	}

	public static SystemBasicSettingsInfo ToSystemBasicSettingsInfo(this RouterConfig config)
	{
		var web = config.WebServiceConfig ?? new WebServiceConfig();
		var cors = web.HttpCrossDomainControlInfo;
		var certificateInfes = web.HttpsCertificateFileInfes;
		if (certificateInfes?.Length > 0 != true && !string.IsNullOrWhiteSpace(web.HttpsCertificateFilePath))
		{
			certificateInfes = [new HttpsCertificateFileInfo
			{
				CertificateFilePath = web.HttpsCertificateFilePath,
				CertificatePasswordFilePath = web.HttpsCertificatePasswordFilePath,
				CertificateFilePassword = web.HttpsCertificateFilePassword
			}];
		}

		return new SystemBasicSettingsInfo
		{
			HttpAddress = web.HttpIPAddress,
			HttpPort = web.HttpPort,
			HttpsAddress = web.HttpsIPAddress,
			HttpsPort = web.HttpsPort,
			ForceHttps = web.IsHttpsNecessary,
			HttpOrigins = ConfigDocumentUtil.JoinValues(cors?.Access_Control_Allow_Origins),
			CorsMethods = cors?.Access_Control_Allow_Methods,
			CorsHeaders = cors?.Access_Control_Allow_Headers,
			CorsCredentials = cors?.Access_Control_Allow_Credentials ?? false,
			WebSocketOrigins = ConfigDocumentUtil.JoinValues(web.WebSocketCrossDomainControlInfo?.Access_Control_Allow_Origins),
			RequestMaxSize = web.HttpRequestBodySizeInMBMax,
			RequestLogEnabled = config.IsHttpRequestLogEnable,
			RequestLogFormat = config.HttpRequestLogContentFormatter,
			RequestHeaders = ConfigDocumentUtil.JoinValues(config.ClientRequestHttpHeaderValidKeys),
			FingerprintHeaders = ConfigDocumentUtil.JoinValues(config.ClientRequestHttpHeaderHashKeys),
			ResponseMaxDuration = config.EndPointRequestTimeoutSecondsMax,
			ResponseHeaders = ConfigDocumentUtil.JoinValues(config.ClientResponseHttpHeaderValidKeys),
			CompressionTypes = ConfigDocumentUtil.JoinValues(config.ClientResponseCompressionContentTypes),
			CompressionMinLength = config.ClientResponseCompressionContentMinLength / 1024.0,
			CompressionAlgorithm = ToCompressionAlgorithm(config.ClientResponseCompressionMethod),
			CacheStatusCodes = string.Join(",", config.EndPointResponseCacheValidHttpStatusCodes ?? []),
			CacheRefreshSeconds = config.ClientResponseCacheAutoUpdateIntervalSeconds,
			CacheKeepSeconds = config.ClientResponseCacheNoneReadSecondsToRelease,
			SmtpAddress = config.SMTPServerAddress,
			SmtpPort = config.SMTPServerPort,
			SmtpSslEnabled = config.IsSMTPServerSSLEnable,
			SmtpAccount = config.SMTPServerAccount,
			HasSmtpPassword = !string.IsNullOrEmpty(config.SMTPServerPassword),
			SenderName = config.SMTPAuthorName,
			Certificates = certificateInfes?.Select((certificate, index) => new SystemCertificateInfo
			{
				Identity = index + 1,
				Domain = certificate.HostName,
				CertificateFileName = certificate.CertificateFilePath,
				PasswordFileName = certificate.CertificatePasswordFilePath,
				CertificateFileExpireTime = certificate.CertificateFileExpireTime == default ? null : certificate.CertificateFileExpireTime
			}).ToArray() ?? []
		};
	}

	public static JsonObject ToRouterConfigDocument(this SystemBasicSettingsInfo info, JsonObject document)
	{
		ConfigDocumentUtil.ValidateBindAddress(info.HttpAddress);
		ConfigDocumentUtil.ValidateBindAddress(info.HttpsAddress);
		ConfigDocumentUtil.Require(info.HttpPort is >= 0 and <= 65535 && info.HttpsPort is >= 0 and <= 65535
			&& info.SmtpPort is >= 0 and <= 65535, "端口必须在 0 到 65535 之间。");
		ConfigDocumentUtil.Require(info.RequestMaxSize >= 0 && info.RequestMaxSize <= long.MaxValue / (1024 * 1024), "请求大小超出有效范围。");
		ConfigDocumentUtil.RequireNonNegative(info.ResponseMaxDuration, "响应最长时长");
		ConfigDocumentUtil.RequireNonNegative(info.CacheKeepSeconds, "缓存保留时长");
		ConfigDocumentUtil.Require(double.IsFinite(info.CacheRefreshSeconds) && info.CacheRefreshSeconds > 0, "缓存更新间隔必须大于 0。");
		ConfigDocumentUtil.RequireNonNegative(info.CompressionMinLength, "压缩最小长度");
		var compressionBytes = info.CompressionMinLength * 1024;
		ConfigDocumentUtil.Require(compressionBytes <= int.MaxValue && compressionBytes == Math.Truncate(compressionBytes), "压缩最小长度必须能转换为整数个字节。");
		var compressionMethod = info.CompressionAlgorithm?.Trim().ToLowerInvariant() switch
		{
			"brotli" or "br" => "br",
			"gzip" => "gzip",
			"deflate" => "deflate",
			"none" => "none",
			"auto" or null or "" => "auto",
			_ => throw new ArgumentException("不支持的压缩算法。")
		};
		var statusCodes = ConfigDocumentUtil.SplitValues(info.CacheStatusCodes).Select(value =>
		{
			ConfigDocumentUtil.Require(int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var code)
				&& code is >= 100 and <= 599, "缓存 HTTP 状态码必须在 100 到 599 之间。");
			return code;
		}).ToArray();
		var web = ConfigDocumentUtil.Section<WebServiceConfig>(document, nameof(RouterConfig.WebServiceConfig));
		ConfigDocumentUtil.Set(web, nameof(WebServiceConfig.HttpIPAddress), info.HttpAddress?.Trim());
		ConfigDocumentUtil.Set(web, nameof(WebServiceConfig.HttpPort), info.HttpPort);
		ConfigDocumentUtil.Set(web, nameof(WebServiceConfig.HttpsIPAddress), info.HttpsAddress?.Trim());
		ConfigDocumentUtil.Set(web, nameof(WebServiceConfig.HttpsPort), info.HttpsPort);
		ConfigDocumentUtil.Set(web, nameof(WebServiceConfig.IsHttpsNecessary), info.ForceHttps);
		ConfigDocumentUtil.Set(web, nameof(WebServiceConfig.HttpRequestBodySizeInMBMax), info.RequestMaxSize);
		var cors = ConfigDocumentUtil.Section<CrossDomainControlInfo>(web, nameof(WebServiceConfig.HttpCrossDomainControlInfo));
		ConfigDocumentUtil.Set(cors, nameof(CrossDomainControlInfo.Access_Control_Allow_Origins), ConfigDocumentUtil.SplitValues(info.HttpOrigins));
		ConfigDocumentUtil.Set(cors, nameof(CrossDomainControlInfo.Access_Control_Allow_Methods), ConfigDocumentUtil.JoinValues(ConfigDocumentUtil.SplitValues(info.CorsMethods)));
		ConfigDocumentUtil.Set(cors, nameof(CrossDomainControlInfo.Access_Control_Allow_Headers), ConfigDocumentUtil.JoinValues(ConfigDocumentUtil.SplitValues(info.CorsHeaders)));
		ConfigDocumentUtil.Set(cors, nameof(CrossDomainControlInfo.Access_Control_Allow_Credentials), info.CorsCredentials);
		var webSocketCors = ConfigDocumentUtil.Section<CrossDomainControlInfo>(web, nameof(WebServiceConfig.WebSocketCrossDomainControlInfo));
		ConfigDocumentUtil.Set(webSocketCors, nameof(CrossDomainControlInfo.Access_Control_Allow_Origins), ConfigDocumentUtil.SplitValues(info.WebSocketOrigins));
		if (info.Certificates != null)
		{
			var originalCertificates = ConfigDocumentUtil.Get<HttpsCertificateFileInfo[]>(web, nameof(WebServiceConfig.HttpsCertificateFileInfes)) ?? [];
			if (originalCertificates.Length == 0 && !string.IsNullOrWhiteSpace(ConfigDocumentUtil.Get<string>(web, nameof(WebServiceConfig.HttpsCertificateFilePath))))
			{
				originalCertificates = [new HttpsCertificateFileInfo
				{
					CertificateFilePassword = ConfigDocumentUtil.Get<string>(web, nameof(WebServiceConfig.HttpsCertificateFilePassword))
				}];
			}
			var certificates = info.Certificates.Select(certificate =>
			{
				ConfigDocumentUtil.Require(certificate != null && (!string.IsNullOrWhiteSpace(certificate.CertificateFileName) || !string.IsNullOrEmpty(certificate.CertificateUploadToken)), "证书文件名不能为空。");
				var original = certificate!.Identity > 0 && certificate.Identity <= originalCertificates.Length ? originalCertificates[certificate.Identity - 1] : null;
				return new HttpsCertificateFileInfo
				{
					HostName = certificate.Domain?.Trim(),
					CertificateFilePath = certificate.CertificateFileName?.Trim(),
					CertificatePasswordFilePath = certificate.PasswordFileName?.Trim(),
					CertificateFilePassword = string.IsNullOrEmpty(certificate.PasswordUploadToken) ? original?.CertificateFilePassword : null,
					CertificateFileExpireTime = certificate.IsCertificateFileExpireTimeCleared ? default : certificate.CertificateFileExpireTime ?? original?.CertificateFileExpireTime ?? default
				};
			}).ToArray();
			ConfigDocumentUtil.Set(web, nameof(WebServiceConfig.HttpsCertificateFileInfes), certificates);
			ConfigDocumentUtil.Set<string?>(web, nameof(WebServiceConfig.HttpsCertificateFilePath), null);
			ConfigDocumentUtil.Set<string?>(web, nameof(WebServiceConfig.HttpsCertificatePasswordFilePath), null);
			ConfigDocumentUtil.Set<string?>(web, nameof(WebServiceConfig.HttpsCertificateFilePassword), null);
		}
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.IsHttpRequestLogEnable), info.RequestLogEnabled);
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.HttpRequestLogContentFormatter), info.RequestLogFormat ?? RouterConfig.HttpRequestLogContentFormatterDefault);
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.ClientRequestHttpHeaderValidKeys), ConfigDocumentUtil.SplitValues(info.RequestHeaders));
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.ClientRequestHttpHeaderHashKeys), ConfigDocumentUtil.SplitValues(info.FingerprintHeaders));
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.EndPointRequestTimeoutSecondsMax), info.ResponseMaxDuration);
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.ClientResponseHttpHeaderValidKeys), ConfigDocumentUtil.SplitValues(info.ResponseHeaders));
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.ClientResponseCompressionContentTypes), ConfigDocumentUtil.SplitValues(info.CompressionTypes));
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.ClientResponseCompressionContentMinLength), (int)compressionBytes);
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.ClientResponseCompressionMethod), compressionMethod);
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.EndPointResponseCacheValidHttpStatusCodes), statusCodes);
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.ClientResponseCacheAutoUpdateIntervalSeconds), info.CacheRefreshSeconds);
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.ClientResponseCacheNoneReadSecondsToRelease), info.CacheKeepSeconds);
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.SMTPServerAddress), info.SmtpAddress?.Trim());
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.SMTPServerPort), info.SmtpPort);
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.IsSMTPServerSSLEnable), info.SmtpSslEnabled);
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.SMTPServerAccount), info.SmtpAccount?.Trim());
		if (info.ClearSmtpPassword || !string.IsNullOrEmpty(info.SmtpPassword))
		{
			ConfigDocumentUtil.Set(document, nameof(RouterConfig.SMTPServerPassword), info.ClearSmtpPassword ? null : info.SmtpPassword);
		}
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.SMTPAuthorName), info.SenderName?.Trim());
		return document;
	}

	#endregion
}
