using Boar.Red.Gateway.Extensions;
using System;
using System.Collections.Generic;

namespace Boar.Red.Gateway.Utils
{
	public class UrlPathSectionMatchRule
	{
		////////////////////////////////////////////////
		// @静态常量
		////////////////////////////////////////////////

		public enum RuleExpressionType
		{
			CompareCharsIgnoreCase = 0,

			FuzzyMatching = 1,

			Expression = 2,
		}

		protected class RuleExpression
		{
			public RuleExpressionType Type { get; set; } = RuleExpressionType.CompareCharsIgnoreCase;

			public string ExpressionContent { get; set; }

			public bool IsFirstExpression { get; set; }

			public bool IsLastExpression { get; set; }

			public RuleExpression(
			    RuleExpressionType type,
			    string expressionContent,
			    bool isFirstExpression,
			    bool isLastExpression)
			{
				this.Type = type;
				this.ExpressionContent = expressionContent;
				this.IsFirstExpression = isFirstExpression;
				this.IsLastExpression = isLastExpression;
			}
		}

		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性

		public UrlPathMatchRuleKeywords? MatchRuleKeywords { get; set; }

		protected string? _matchRule;

		protected List<RuleExpression>? _ruleExpressions;

		public string? MatchRule
		{
			get
			{
				return _matchRule;
			}
			set
			{
				_matchRule = value;
				if (_ruleExpressions == null)
				{
					_ruleExpressions = new List<RuleExpression>();
				}
				else
				{
					_ruleExpressions.Clear();
				}

				string fuzzyMatchingKey = UrlPathMatchRuleKeywords.FuzzyMatchingKeyDefault;
				string matchExpressionBeginSign = UrlPathMatchRuleKeywords.MatchExpressionBeginSignDefault;
				string matchExpressionEndSign = UrlPathMatchRuleKeywords.MatchExpressionEndSignDefault;

				var matchRuleKeywords = this.MatchRuleKeywords;
				if (matchRuleKeywords != null)
				{
					fuzzyMatchingKey = matchRuleKeywords.FuzzyMatchingKey;
					matchExpressionBeginSign = matchRuleKeywords.MatchExpressionBeginSign;
					matchExpressionEndSign = matchRuleKeywords.MatchExpressionEndSign;
				}


				for (var matchRuleCharIndex = 0;
				   matchRuleCharIndex < _matchRule?.Length;)
				{
					RuleExpressionType expressionType = RuleExpressionType.CompareCharsIgnoreCase;
					int expressionBeginIndex = matchRuleCharIndex;
					// !!!
					int expressionEndIndex = matchRuleCharIndex + 1;
					// !!!

					for (;
					    matchRuleCharIndex < _matchRule.Length;
					    matchRuleCharIndex++)
					{
						if (expressionType == RuleExpressionType.CompareCharsIgnoreCase
						    && fuzzyMatchingKey.Length > 0
						    && CharExtension.IsCharsOfStringEqualsKey(
						    fuzzyMatchingKey,
						    _matchRule,
						    matchRuleCharIndex) == true)
						{
							// !!!
							expressionType = RuleExpressionType.FuzzyMatching;
							expressionEndIndex = matchRuleCharIndex + fuzzyMatchingKey.Length;
							break;
							// !!!
						}
						if (expressionType == RuleExpressionType.CompareCharsIgnoreCase
						    && matchExpressionBeginSign.Length > 0
						    && CharExtension.IsCharsOfStringEqualsKey(
						    matchExpressionBeginSign,
						    _matchRule,
						    matchRuleCharIndex) == true)
						{
							// !!!
							expressionType = RuleExpressionType.Expression;
							// !!!
							var matchExpressionEndSignIndex = -1;
							if ((matchRuleCharIndex + 1 + matchExpressionEndSign.Length)
							    <= _matchRule.Length)
							{
								matchExpressionEndSignIndex
								    = _matchRule.IndexOf(
								    matchExpressionEndSign,
								    matchRuleCharIndex + 1,
								    System.StringComparison.OrdinalIgnoreCase);
							}
							if (matchExpressionEndSignIndex < 0)
							{
								expressionEndIndex = _matchRule.Length;
							}
							else
							{
								// !!!
								expressionEndIndex = matchExpressionEndSignIndex + matchExpressionEndSign.Length;
								// !!!
							}
							// !!!
							break;
							// !!!
						}
						if (expressionType == RuleExpressionType.CompareCharsIgnoreCase)
						{
							expressionEndIndex = matchRuleCharIndex + 1;
						}
					}
					// !!!
					matchRuleCharIndex = expressionEndIndex;
					// !!!


					var expressionContent = _matchRule[expressionBeginIndex..expressionEndIndex];

					var expression = new RuleExpression(
					    expressionType,
					    expressionContent,
					    _ruleExpressions.Count < 1,
					    expressionEndIndex == _matchRule.Length);
					{ }
					_ruleExpressions.Add(expression);
				}
			}
		}

		protected RuleExpression? FindNextExpressionFrom(
		    List<RuleExpression> ruleExpressions,
		    int beginExpressionIndex,
		    RuleExpressionType objectExpressionType)
		{
			if (ruleExpressions == null)
			{
				return null;
			}

			for (var expressionIndex = beginExpressionIndex;
			    expressionIndex < ruleExpressions.Count;
			    expressionIndex++)
			{
				var ruleExpression = ruleExpressions[expressionIndex];
				if (ruleExpression.Type == objectExpressionType)
				{
					return ruleExpression;
				}
			}
			return null;
		}

		public bool IsMatched(string urlPath)
		{
			if (urlPath == null)
			{
				return false;
			}
			if (urlPath.Length < 1)
			{
				return false;
			}

			if (_ruleExpressions == null
			    || _ruleExpressions.Count < 1)
			{
				return false;
			}


			var isMatched = true;
			var urlPathMatchBeginCharIndex = 0;
			var isInFuzzyMatching = false;
			for (var ruleExpressionIndex = 0;
			    ruleExpressionIndex < _ruleExpressions.Count;
			    ruleExpressionIndex++)
			{
				var ruleExpression = _ruleExpressions[ruleExpressionIndex];
				switch (ruleExpression.Type)
				{
					default:
					case RuleExpressionType.CompareCharsIgnoreCase:
						{
							// 字符匹配成功：
							if (CharExtension.IsCharsOfStringEqualsKey(
							    ruleExpression.ExpressionContent,
							    urlPath,
							    urlPathMatchBeginCharIndex)
							    == true)
							{
								// !!!🎉 匹配成功 🎉!!!
								urlPathMatchBeginCharIndex += ruleExpression.ExpressionContent.Length;
								isInFuzzyMatching = false;
								// !!!

								// !!!⚠ 明确指定了结尾内容的，严格执行 ⚠!!!
								// 不在此处进行【匹配完整度】的判断，交由尾端判断。
								//if (ruleExpression.IsLastExpression == true
								//        && urlPathMatchBeginCharIndex < urlPath.Length)
								//{
								//        isMatched = false;
								//}
							}
							else if (isInFuzzyMatching == true)
							{
								// !!!🎉 匹配成功 🎉!!!
								urlPathMatchBeginCharIndex += 1;
								// !!!
							}
							else
							{
								isMatched = false;
							}
						}
						break;
					case RuleExpressionType.FuzzyMatching:
						{
							// !!!
							isInFuzzyMatching = true;
							// !!!
						}
						break;
					case RuleExpressionType.Expression:
						{
							var nextCompareCharsIgnoreCaseExpression
							    = this.FindNextExpressionFrom(
							    _ruleExpressions,
							    urlPathMatchBeginCharIndex,
							    RuleExpressionType.CompareCharsIgnoreCase);
							var urlPathMatchEndCharIndex = urlPath.Length;
							if (nextCompareCharsIgnoreCaseExpression != null)
							{
								urlPathMatchEndCharIndex = urlPath.IndexOf(
								    nextCompareCharsIgnoreCaseExpression.ExpressionContent,
								    urlPathMatchBeginCharIndex,
								    StringComparison.OrdinalIgnoreCase);
								// !!! 没有找到后续的常量表达式，则直接返回匹配失败 !!!
								if (urlPathMatchEndCharIndex < 0)
								{
									isMatched = false;
								}
							}
							if (isMatched)
							{
								if (this.DidIsCharsInStringMatchedExpression(
								    ruleExpression.ExpressionContent,
								    urlPath,
								    urlPathMatchBeginCharIndex,
								    urlPathMatchEndCharIndex))
								{
									// !!!🎉 匹配成功 🎉!!!
									urlPathMatchBeginCharIndex = urlPathMatchEndCharIndex;
									isInFuzzyMatching = false;
									// !!!
								}
								else if (isInFuzzyMatching)
								{
									// !!!🎉 匹配成功 🎉!!!
									urlPathMatchBeginCharIndex += 1;
									// !!!
								}
								else
								{
									isMatched = false;
								}
							}
						}
						break;
				}
				if (isMatched != true)
				{

					return false;
				}
			}
			if (urlPathMatchBeginCharIndex < urlPath.Length)
			{
				return false;
			}
			return isMatched;
		}


		#endregion


		////////////////////////////////////////////////
		// @事件节点
		////////////////////////////////////////////////

		#region 事件节点

		protected virtual bool DidIsCharsInStringMatchedExpression(
		    string expression,
		    string str,
		    int charsBeginIndex,
		    int charsEndIndex)
		{
			return true;
		}

		protected virtual bool DidGetContentLengthMatchedInUrlPathWithExpression(
		    string urlPath,
		    int urlPathMatchBeginCharIndex,
		    string expressionContent)
		{
			return true;
		}

		#endregion

	}
}
