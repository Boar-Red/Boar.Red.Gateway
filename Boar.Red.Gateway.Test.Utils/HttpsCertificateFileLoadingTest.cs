using Boar.Red.Service.WebService.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Boar.Red.Gateway.Test.Utils;

[TestClass]
[DoNotParallelize]
public class HttpsCertificateFileLoadingTest
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[TestMethod]
	public void RelativeAndAbsoluteCertificateAndPasswordPathsLoadOutsideWorkingDirectory()
	{
		var applicationDirectory = string.IsNullOrEmpty(BaoXia.Utils.Environment.ApplicationDirectoryPath)
			? AppContext.BaseDirectory : BaoXia.Utils.Environment.ApplicationDirectoryPath;
		var directoryName = "CertificateLoadingTest_" + Guid.NewGuid().ToString("N");
		var directory = Path.Combine(applicationDirectory, directoryName);
		var originalWorkingDirectory = System.Environment.CurrentDirectory;
		var originalCache = WebServiceConfig.GetHttpsCertificateFileInfesCache();
		Directory.CreateDirectory(directory);
		try
		{
			using var key = RSA.Create(2048);
			var request = new CertificateRequest("CN=example.test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
			File.WriteAllBytes(Path.Combine(directory, "server.pfx"), certificate.Export(X509ContentType.Pfx, "test-password"));
			File.WriteAllText(Path.Combine(directory, "password.txt"), "test-password");
			System.Environment.CurrentDirectory = Path.GetTempPath();
			foreach (var isAbsolute in new[] { false, true })
			{
				var prefix = isAbsolute ? directory : directoryName;
				var info = new HttpsCertificateFileInfo
				{
					HostName = "example.test",
					CertificateFilePath = Path.Combine(prefix, "server.pfx"),
					CertificatePasswordFilePath = Path.Combine(prefix, "password.txt")
				};
				var cache = WebServiceConfig.SetHttpsCertificateFileInfesCache([info], null);
				Assert.HasCount(1, cache!);
				Assert.AreEqual(certificate.Thumbprint, cache![0].X509Certificate2!.Thumbprint);
				cache[0].X509Certificate2!.Dispose();
			}
		}
		finally
		{
			System.Environment.CurrentDirectory = originalWorkingDirectory;
			typeof(WebServiceConfig).GetField("_httpsCertificateFileInfesCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
				.SetValue(null, originalCache);
			var expectedDirectory = Path.GetFullPath(Path.Combine(applicationDirectory, directoryName));
			if (Path.GetFullPath(directory) == expectedDirectory && Path.GetFileName(directory).StartsWith("CertificateLoadingTest_", StringComparison.Ordinal))
			{
				Directory.Delete(directory, true);
			}
		}
	}

	#endregion
}
