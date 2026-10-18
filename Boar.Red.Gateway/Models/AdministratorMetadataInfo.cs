namespace Boar.Red.Gateway.Models;

/// <summary>管理页面显示信息；认证账号、密码仍由 ServiceAdministratorConfig 保存。</summary>
public class AdministratorMetadataInfo
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public long Identity { get; set; }
	public string? Account { get; set; }
	public string? Name { get; set; }

	#endregion
}
