namespace Boar.Red.Gateway.Constants
{
	public class ResponseBodies
	{
		public const string Http_200_TestCompleted = "成功：测试成功！";

		public const string Http_404_NoneRouteRuleMatched = "错误：没有匹配成功的路由规则。";

		public const string Http_404_CreateResponseCacheFailed = "错误：创建响应缓存失败。";

		public const string Http_429_ClientRequestRateLimitExceeded = "错误：当前请求频率超出限制。";

		public const string Http_500_RouteFailedWithProgramError = "错误：路由失败，程序异常。";
	}
}
