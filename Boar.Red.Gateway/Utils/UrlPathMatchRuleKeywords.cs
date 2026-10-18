using Boar.Red.Gateway.Constants;

namespace Boar.Red.Gateway.Utils
{
	public class UrlPathMatchRuleKeywords
	{

		////////////////////////////////////////////////
		// @静态常量
		////////////////////////////////////////////////

		/// <summary>
		/// Url中的路由关键字，模糊匹配：*。
		/// </summary>
		public static readonly string FuzzyMatchingKeyDefault = PathKeywords.FuzzyMatchingKey;

		/// <summary>
		/// Url中的匹配表达式，起始字符：[。
		/// </summary>
		public static readonly string MatchExpressionBeginSignDefault = PathKeywords.MatchExpressionBeginSign;

		/// <summary>
		/// Url中的匹配表达式，结束字符：[。
		/// </summary>
		public static readonly string MatchExpressionEndSignDefault = PathKeywords.MatchExpressionEndSign;

		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		public string FuzzyMatchingKey { get; set; } = FuzzyMatchingKeyDefault;

		public string MatchExpressionBeginSign { get; set; } = MatchExpressionBeginSignDefault;

		public string MatchExpressionEndSign { get; set; } = MatchExpressionEndSignDefault;
	}
}
