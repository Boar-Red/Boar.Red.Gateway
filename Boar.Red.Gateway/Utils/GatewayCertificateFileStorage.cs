using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.LogFiles;
using Boar.Red.Gateway.ViewModels;
using Boar.Red.Service.WebService.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Utils;

public sealed class GatewayCertificateFileStorage : IDisposable
{
	////////////////////////////////////////////////
	// @静态常量
	////////////////////////////////////////////////

	#region 静态常量

	public const long FileSizeMax = 5 * 1024 * 1024;
	public static readonly TimeSpan TemporaryFileLifetime = TimeSpan.FromHours(24);

	#endregion

	////////////////////////////////////////////////
	// @静态变量
	////////////////////////////////////////////////

	#region 静态变量

	private static readonly Lazy<GatewayCertificateFileStorage> _default = new(() => new(
		string.IsNullOrEmpty(BaoXia.Utils.Environment.ApplicationDirectoryPath) ? AppContext.BaseDirectory : BaoXia.Utils.Environment.ApplicationDirectoryPath));

	#endregion

	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	private readonly object _syncRoot = new();
	private readonly string _applicationDirectory;
	private readonly Timer _cleanupTimer;

	public static GatewayCertificateFileStorage Default => _default.Value;

	public string TemporaryDirectory { get; }

	#endregion

	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public GatewayCertificateFileStorage(string applicationDirectory, string? temporaryDirectory = null)
	{
		_applicationDirectory = Path.GetFullPath(applicationDirectory);
		// 系统临时目录由运行平台决定；按程序目录隔离不同网关的临时证书。
		var directoryKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_applicationDirectory)));
		TemporaryDirectory = Path.GetFullPath(temporaryDirectory ?? Path.Combine(Path.GetTempPath(), "Boar.Red.Gateway.CertificateUploads", directoryKey));
		_cleanupTimer = new Timer(_ =>
		{
			try
			{
				CleanupExpiredFiles();
			}
			catch (Exception exception)
			{
				Log.Exception.Logs(this, "清理过期证书临时文件失败。", exception);
			}
		}, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
	}

	private static void CreatePrivateDirectory(string path)
	{
		if (OperatingSystem.IsWindows())
		{
			Directory.CreateDirectory(path);
		}
		else
		{
			Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}
	}

	private static void DeleteFileIfExists(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (IOException)
		{ }
		catch (UnauthorizedAccessException)
		{ }
	}

	private static string GetUploadFileName(string fileName, string extension)
	{
		var name = Path.GetFileName(fileName.Replace('\\', '/'));
		ConfigDocumentUtil.Require(!string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
			&& !name.EndsWith(' ') && !name.EndsWith('.') && string.Equals(Path.GetExtension(name), extension, StringComparison.OrdinalIgnoreCase), "上传文件名无效。");
		foreach (var character in name)
		{
			ConfigDocumentUtil.Require(character >= ' ' && "<>:\"|?*".IndexOf(character) < 0, "上传文件名包含跨平台不支持的字符。");
		}
		var deviceName = name.Split('.')[0].ToUpperInvariant();
		ConfigDocumentUtil.Require(deviceName is not ("CON" or "PRN" or "AUX" or "NUL")
			&& !(deviceName.Length == 4 && (deviceName.StartsWith("COM", StringComparison.Ordinal) || deviceName.StartsWith("LPT", StringComparison.Ordinal))
				&& deviceName[3] is >= '1' and <= '9'), "上传文件名属于系统保留名称。");
		return name;
	}

	private static void DeleteTemporaryFile(string path)
	{
		DeleteFileIfExists(path);
		DeleteFileIfExists(path + ".name");
	}

	private string GetTemporaryFilePath(string token, string extension)
	{
		// 只接收服务端签发的文件标识，客户端不能提供临时目录或目标路径。
		ConfigDocumentUtil.Require(token.Length == 32 + extension.Length && token.EndsWith(extension, StringComparison.Ordinal)
			&& Guid.TryParseExact(token[..32], "N", out _), "证书上传标识无效，请重新上传文件。");
		var path = Path.Combine(TemporaryDirectory, token);
		ConfigDocumentUtil.Require(File.Exists(path) && File.GetLastWriteTimeUtc(path) > DateTime.UtcNow - TemporaryFileLifetime,
			"证书临时文件已过期或不存在，请重新上传文件。");
		return path;
	}

	public void CleanupExpiredFiles()
	{
		lock (_syncRoot)
		{
			if (!Directory.Exists(TemporaryDirectory))
			{
				return;
			}
			var cutoff = DateTime.UtcNow - TemporaryFileLifetime;
			foreach (var path in Directory.EnumerateFiles(TemporaryDirectory))
			{
				var name = Path.GetFileNameWithoutExtension(path);
				var extension = Path.GetExtension(path);
				if (Guid.TryParseExact(name, "N", out _) && extension is ".pfx" or ".txt" && File.GetLastWriteTimeUtc(path) <= cutoff)
				{
					DeleteTemporaryFile(path);
				}
				else if (extension == ".name" && !File.Exists(path[..^5]) && File.GetLastWriteTimeUtc(path) <= cutoff)
				{
					var token = Path.GetFileNameWithoutExtension(path);
					if (token.Length == 36 && Guid.TryParseExact(token[..32], "N", out _) && token[32..] is ".pfx" or ".txt")
					{
						DeleteFileIfExists(path);
					}
				}
			}
		}
	}

	public async Task<CertificateFileUploadInfo> UploadAsync(Stream source, string fileName, long length, CancellationToken cancellationToken = default)
	{
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		ConfigDocumentUtil.Require(extension is ".pfx" or ".txt" && length > 0 && length <= FileSizeMax, "请上传不超过 5 MB 的 pfx 证书文件或 txt 密码文件。");
		var originalFileName = GetUploadFileName(fileName, extension);
		lock (_syncRoot)
		{
			CreatePrivateDirectory(TemporaryDirectory);
			CleanupExpiredFiles();
		}
		var token = Guid.NewGuid().ToString("N") + extension;
		var path = Path.Combine(TemporaryDirectory, token);
		var uploadingPath = path + ".uploading";
		try
		{
			await using (var target = new FileStream(uploadingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
			{
				if (!OperatingSystem.IsWindows())
				{
					File.SetUnixFileMode(uploadingPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
				}
				var buffer = new byte[81920];
				long copiedLength = 0;
				int readLength;
				while ((readLength = await source.ReadAsync(buffer, cancellationToken)) > 0)
				{
					copiedLength += readLength;
					ConfigDocumentUtil.Require(copiedLength <= FileSizeMax, "上传文件不能超过 5 MB。");
					await target.WriteAsync(buffer.AsMemory(0, readLength), cancellationToken);
				}
				ConfigDocumentUtil.Require(copiedLength == length, "上传文件内容不完整，请重新上传。");
			}
			// 标识用于定位临时文件，原始文件名由服务端保存，不依赖客户端再次提供的名称。
			await File.WriteAllTextAsync(path + ".name", originalFileName, new UTF8Encoding(false), cancellationToken);
			File.Move(uploadingPath, path);
			return new CertificateFileUploadInfo { UploadToken = token, FileName = originalFileName };
		}
		catch
		{
			DeleteTemporaryFile(path);
			throw;
		}
		finally
		{
			DeleteFileIfExists(uploadingPath);
		}
	}

	public void SaveConfigurationFiles(SystemCertificateInfo[]? certificates, JsonObject document, Action<JsonObject> saveDocument)
	{
		lock (_syncRoot)
		{
			var files = new Dictionary<string, string>(StringComparer.Ordinal);
			var copiedFiles = new List<string>();
			var web = ConfigDocumentUtil.Section<WebServiceConfig>(document, nameof(RouterConfig.WebServiceConfig));
			var certificateNodes = web[nameof(WebServiceConfig.HttpsCertificateFileInfes)] as JsonArray;
			string? ResolveFile(string? token, string extension, string? existingPath)
			{
				if (string.IsNullOrEmpty(token))
				{
					return existingPath;
				}
				var source = GetTemporaryFilePath(token, extension);
				if (!files.TryGetValue(source, out var relativePath))
				{
					var originalFileName = File.Exists(source + ".name") ? File.ReadAllText(source + ".name", Encoding.UTF8) : existingPath ?? Path.GetFileName(source);
					originalFileName = GetUploadFileName(originalFileName, extension);
					// 独立目录避免同名证书相互覆盖，文件本身保持上传时的名称。
					relativePath = "CertificateFiles/" + Guid.NewGuid().ToString("N") + "/" + originalFileName;
					files.Add(source, relativePath);
				}
				return relativePath;
			}
			for (var index = 0; index < (certificates?.Length ?? 0); index++)
			{
				var certificate = certificates![index];
				var node = (JsonObject)certificateNodes![index]!;
				ConfigDocumentUtil.Set(node, nameof(HttpsCertificateFileInfo.CertificateFilePath),
					ResolveFile(certificate.CertificateUploadToken, ".pfx", certificate.CertificateFileName?.Trim()));
				ConfigDocumentUtil.Set(node, nameof(HttpsCertificateFileInfo.CertificatePasswordFilePath),
					ResolveFile(certificate.PasswordUploadToken, ".txt", certificate.PasswordFileName?.Trim()));
			}
			try
			{
				if (files.Count > 0)
				{
					CreatePrivateDirectory(Path.Combine(_applicationDirectory, "CertificateFiles"));
				}
				foreach (var file in files)
				{
					var targetPath = Path.Combine(_applicationDirectory, file.Value);
					copiedFiles.Add(targetPath);
					CreatePrivateDirectory(Path.GetDirectoryName(targetPath)!);
					// 临时目录与程序目录可能位于不同磁盘，先复制；配置提交成功后再移除临时文件。
					File.Copy(file.Key, targetPath, false);
				}
				saveDocument(document);
			}
			catch
			{
				foreach (var path in copiedFiles)
				{
					DeleteFileIfExists(path);
					var directory = Path.GetDirectoryName(path)!;
					if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
					{
						Directory.Delete(directory);
					}
				}
				throw;
			}
			foreach (var source in files.Keys)
			{
				DeleteTemporaryFile(source);
			}
		}
	}

	public void Dispose()
	{
		_cleanupTimer?.Dispose();
	}

	#endregion
}
