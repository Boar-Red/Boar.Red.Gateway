using System;
using System.Text.Json.Serialization;

namespace Boar.Red.Gateway.ViewModels;

public class SystemCertificateInfo
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public long Identity { get; set; }

	public string? Domain { get; set; }

	public string? CertificateFileName { get; set; }

	public string? PasswordFileName { get; set; }

	/// <summary>未设置的时间不输出，避免客户端把空时间解析为当前日期。</summary>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public DateTimeOffset? CertificateFileExpireTime { get; set; }

	/// <summary>显式清空时间；未提交时间及此标记的旧客户端保留原值。</summary>
	public bool IsCertificateFileExpireTimeCleared { get; set; }

	/// <summary>本次上传的 pfx 临时文件标识，保存成功后清除。</summary>
	public string? CertificateUploadToken { get; set; }

	/// <summary>本次上传的 txt 临时文件标识，保存成功后清除。</summary>
	public string? PasswordUploadToken { get; set; }

	#endregion
}
