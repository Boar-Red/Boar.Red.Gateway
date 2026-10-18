namespace Boar.Red.Gateway.ConfigFiles;

public class Config
{
	////////////////////////////////////////////////
	// @静态变量
	////////////////////////////////////////////////

	#region 静态变量

	/// <summary>
	/// 路由服务配置文件。
	/// </summary>
	public static RouterConfig Router { get; set; } = new();

	/// <summary>
	/// 屏蔽记录配置文件。
	/// </summary>
	public static BlockRecordConfig BlockRecord { get; set; } = new();

	#endregion
}
