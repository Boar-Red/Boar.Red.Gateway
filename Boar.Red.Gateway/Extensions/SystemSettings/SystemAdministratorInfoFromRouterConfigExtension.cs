using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Models;
using Boar.Red.Gateway.Utils;
using Boar.Red.Gateway.ViewModels;
using Boar.Red.Service.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Boar.Red.Gateway.Extensions.SystemSettings;

public static class SystemAdministratorInfoFromRouterConfigExtension
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public static SystemAdministratorInfo[] ToSystemAdministratorInfes(this RouterConfig config)
	{
		var metadataInfes = config.AdministratorMetadataInfes ?? [];
		return (config.ServiceAdministratorConfig?.Administrators ?? []).Select((administrator, index) =>
		{
			var metadata = metadataInfes.FirstOrDefault(info => string.Equals(info.Account, administrator.Account, StringComparison.OrdinalIgnoreCase));
			return new SystemAdministratorInfo
			{
				Identity = metadata?.Identity ?? index + 1,
				Name = metadata?.Name ?? administrator.Account,
				Account = administrator.Account,
				HasPassword = !string.IsNullOrEmpty(administrator.Password),
				ExpireTime = administrator.ExpireTime,
				Remark = administrator.Remark
			};
		}).ToArray();
	}

	public static JsonObject ToRouterConfigDocument(this SaveAdministratorsRequest request, JsonObject document)
	{
		ConfigDocumentUtil.Require(request.Administrators != null, "必须提供完整管理员数组。");
		var administratorConfig = ConfigDocumentUtil.Section<ServiceAdministratorConfig>(document, nameof(RouterConfig.ServiceAdministratorConfig));
		var originalAdministrators = ConfigDocumentUtil.Get<ServiceAdministrator[]>(administratorConfig, nameof(ServiceAdministratorConfig.Administrators)) ?? [];
		var originalMetadata = ConfigDocumentUtil.Get<AdministratorMetadataInfo[]>(document, nameof(RouterConfig.AdministratorMetadataInfes)) ?? [];
		var accounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var identities = new HashSet<long>();
		var administrators = new List<ServiceAdministrator>();
		var metadataInfes = new List<AdministratorMetadataInfo>();
		var nextIdentity = Math.Max(originalMetadata.Select(info => info.Identity).DefaultIfEmpty(0).Max(), originalAdministrators.Length);
		foreach (var info in request.Administrators!)
		{
			ConfigDocumentUtil.Require(info != null && !string.IsNullOrWhiteSpace(info.Account) && !string.IsNullOrWhiteSpace(info.Name), "管理员名称和账号不能为空。");
			var account = info!.Account!.Trim();
			ConfigDocumentUtil.Require(accounts.Add(account), "管理员账号重复。");
			ConfigDocumentUtil.Require(info.ExpireTime > DateTime.MinValue, "管理员到期时间无效。");
			var identity = info.Identity > 0 ? info.Identity : checked(++nextIdentity);
			ConfigDocumentUtil.Require(identity <= 9007199254740991 && identities.Add(identity), "管理员标识无效或重复。");
			nextIdentity = Math.Max(nextIdentity, identity);
			var originalAccount = originalMetadata.FirstOrDefault(metadata => metadata.Identity == identity)?.Account;
			if (originalAccount == null && identity <= originalAdministrators.Length)
			{
				originalAccount = originalAdministrators[identity - 1].Account;
			}
			var original = originalAdministrators.FirstOrDefault(administrator => string.Equals(administrator.Account, originalAccount ?? account, StringComparison.OrdinalIgnoreCase));
			var password = string.IsNullOrEmpty(info.Password) ? original?.Password : info.Password;
			ConfigDocumentUtil.Require(!string.IsNullOrWhiteSpace(password), "新管理员必须设置非空密码。");
			administrators.Add(new ServiceAdministrator
			{
				Account = account,
				Password = password,
				EMail = original?.EMail,
				ExpireTime = info.ExpireTime,
				Remark = info.Remark
			});
			metadataInfes.Add(new AdministratorMetadataInfo { Identity = identity, Account = account, Name = info.Name!.Trim() });
		}
		ConfigDocumentUtil.Require(administrators.Any(administrator => administrator.IsValid), "至少保留一个未过期、可登录的管理员。");
		ConfigDocumentUtil.Set(administratorConfig, nameof(ServiceAdministratorConfig.Administrators), administrators);
		ConfigDocumentUtil.Set(document, nameof(RouterConfig.AdministratorMetadataInfes), metadataInfes);
		return document;
	}


	#endregion
}
