namespace Boar.Red.Gateway.Constants;

public enum EndPointRoutePolicy
{
	/// <summary>
	/// 手动策略，选区第一个标记为可用的终点进行转发。
	/// </summary>
	Manual = 0,

	/// <summary>
	/// 随机策略，随机选取一个终点进行转发。
	/// </summary>
	Random = 1,

	/// <summary>
	/// 主从模式，随机挑选一个终点进行转发，故障时自动切换其他终点。
	/// </summary>
	MasterSlave = 2
}
