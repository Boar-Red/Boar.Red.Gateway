using System;

namespace Boar.Red.Gateway.ViewModels;

public class SystemAdministratorInfo
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public long Identity { get; set; }
	public string? Name { get; set; }
	public string? Account { get; set; }

	/// <summary>查询时不返回密码；保存已有账号时空值保留原密码。</summary>
	public string? Password { get; set; }
	public bool HasPassword { get; set; }
	public DateTime ExpireTime { get; set; }
	public string? Remark { get; set; }

	#endregion
}
