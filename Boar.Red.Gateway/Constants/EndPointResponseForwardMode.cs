namespace Boar.Red.Gateway.Constants;

/// <summary>
/// 终点响应转发模式。
/// </summary>
public enum EndPointResponseForwardMode
{
	/// <summary>
	/// 等待收到终点的完整响应后，再发送给客户端。
	/// </summary>
	WaitForResponseCompleted = 0,

	/// <summary>
	/// 按指定字节数接收并立即转发给客户端。
	/// </summary>
	ForwardByBytes = 1,

	/// <summary>
	/// 使用 Server-Sent Events 协议将终点响应实时转发给客户端。
	/// </summary>
	ServerSentEvents = 2
}
