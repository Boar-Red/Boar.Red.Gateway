
namespace Boar.Red.Gateway.Constants
{
	public class PathKeywords
	{
		/// <summary>
		/// 协议分隔符。
		/// </summary>
		public const string SchemeSpliter = "://";

		/// <summary>
		/// 路径分隔符号，斜线。
		/// </summary>
		public const char PathSymbol_Slash = '\\';

		/// <summary>
		/// 路径分隔符号，反斜线。
		/// </summary>
		public const char PathSymbol_Backslash = '/';


		/// <summary>
		/// 查询标志符号，问号。
		/// </summary>
		public const char QuerySymbol = '?';

		/// <summary>
		/// 查询参数分隔符。
		/// </summary>
		public const char QueryParamsSpliter = '&';

		/// <summary>
		/// 路径分隔符数组。
		/// </summary>
		public static readonly char[] PathSpliterChars =
		[
		    PathSymbol_Slash,
	PathSymbol_Backslash,
	QuerySymbol
		];


		/// <summary>
		/// Url中的路由关键字，模糊匹配：*。
		/// </summary>
		public const string FuzzyMatchingKey = "*";

		/// <summary>
		/// Url中的匹配表达式，起始字符：[。
		/// </summary>
		public const string MatchExpressionBeginSign = "[";

		/// <summary>
		/// Url中的匹配表达式，结束字符：[。
		/// </summary>
		public const string MatchExpressionEndSign = "]";

		/// <summary>
		/// 请求 URL 的默认路由目录前缀；实际值使用 RouterConfig.RouteRequestPathSpliter，重启后生效。
		/// </summary>
		public const string RouteRequestPathSpliter = "@";

		/// <summary>配置规则中的路由目录标记，固定为 @，与请求 URL 前缀独立。</summary>
		public const string RouteRuleCatalogPathPrefix = "@";

		/// <summary>
		/// Url中路由名称分隔符号，前缀，默认为："/@"。
		/// </summary>
		public const string RouteRequestPathSpliterPrefix = "/" + RouteRequestPathSpliter;

		/// <summary>
		/// Url中路由名称分隔符号，后缀，默认为："/"。
		/// </summary>
		public const string RouteRequestPathSpliterSuffix = "/";


		/// <summary>
		/// 客户端路由请求Url中的请求路径片段关键字：$PathSegment。
		/// </summary>
		public const string RouteRequestPathSegment = "$PathSegment";

		/// <summary>
		/// 终点Url中的路由关键字，客户端请求的相对路径：#Path#。
		/// </summary>
		public const string RouteRuleEndPointKey_RelativePath = "#Path#";

		/// <summary>
		/// Url中的路由分组名称，路由记录名字：@RouterRequestRootPath。
		/// </summary>
		public const string SystemRouteValueKey_RouteRequestCatalogPath = "__routeRequestCatalogPath__";

		/// <summary>
		/// Url中的路由关键字，路由记录名字：#Path#。
		/// </summary>
		public const string SystemRouteValueKey_RouteRequestPath = "__routeRequestPath__";
	}
}
