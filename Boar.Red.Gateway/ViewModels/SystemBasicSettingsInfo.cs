namespace Boar.Red.Gateway.ViewModels;

public class SystemBasicSettingsInfo
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public string? HttpAddress { get; set; }
	public int HttpPort { get; set; }
	public string? HttpsAddress { get; set; }
	public int HttpsPort { get; set; }
	public bool ForceHttps { get; set; }
	public string? HttpOrigins { get; set; }
	public string? CorsMethods { get; set; }
	public string? CorsHeaders { get; set; }
	public bool CorsCredentials { get; set; }
	public string? WebSocketOrigins { get; set; }

	/// <summary>请求内容上限，单位为 MB；0 为不限制。</summary>
	public long RequestMaxSize { get; set; }
	public bool RequestLogEnabled { get; set; }
	public string? RequestLogFormat { get; set; }
	public string? RequestHeaders { get; set; }
	public string? FingerprintHeaders { get; set; }
	public double ResponseMaxDuration { get; set; }
	public string? ResponseHeaders { get; set; }
	public string? CompressionTypes { get; set; }

	/// <summary>压缩最小内容长度，单位为 KB。</summary>
	public double CompressionMinLength { get; set; }
	public string? CompressionAlgorithm { get; set; }
	public string? CacheStatusCodes { get; set; }

	/// <summary>自动更新缓存的检查间隔，单位为秒；路由规则可单独设置缓存有效期。</summary>
	public double CacheRefreshSeconds { get; set; }
	public double CacheKeepSeconds { get; set; }
	public string? SmtpAddress { get; set; }
	public int SmtpPort { get; set; }
	public bool SmtpSslEnabled { get; set; }
	public string? SmtpAccount { get; set; }

	/// <summary>查询时不返回密码；保存时空值保留原密码。</summary>
	public string? SmtpPassword { get; set; }
	public bool HasSmtpPassword { get; set; }
	public bool ClearSmtpPassword { get; set; }
	public string? SenderName { get; set; }
	public SystemCertificateInfo[]? Certificates { get; set; }

	#endregion
}
