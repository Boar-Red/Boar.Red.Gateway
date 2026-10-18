namespace Boar.Red.Gateway.ViewModels;

public class SystemAdvancedSettingsInfo
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public string? AdministratorLoginIpWhitelist { get; set; }

	/// <summary>保存的 URL 前缀；未提供时保留原配置，空字符串表示无前缀。</summary>
	public string? RouteRequestPathSpliter { get; set; }

	/// <summary>当前进程实际使用的 URL 前缀，用于提示是否需要重启。</summary>
	public string? EffectiveRouteRequestPathSpliter { get; set; }

	#endregion
}
