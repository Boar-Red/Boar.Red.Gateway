using System;

namespace Boar.Red.Gateway.Extensions
{
	public static class DoubleExtension
	{
		/// <summary>
		/// 获取当前双精度数的小数位数。
		/// </summary>
		/// <param name="doubleValue">指定的双精度数。</param>
		/// <returns>指定双精度数双精度数的小数位数。</returns>
		public static int GetDecimalDigits(this Double doubleValue)
		{

			// 相关资料：
			// https://docs.microsoft.com/zh-cn/dotnet/standard/base-types/standard-numeric-format-strings
			// https://docs.microsoft.com/zh-cn/dotnet/standard/base-types/custom-numeric-format-strings
			// 默认 ( "G" ) 格式
			// “G”或“g”	常规	结果:更紧凑的定点表示法或科学记数法。
			// 受以下类型支持：所有数值类型。
			// 精度说明符：有效位数。
			// 默认值精度说明符：具体取决于数值类型。
			// 更多信息：常规（“G”）格式说明符。
			var doubleValueString = doubleValue.ToString("G");
			var dotIndex = doubleValueString.IndexOf(".");
			var doubleValueDecimalDigits = 0;
			if (dotIndex >= 0)
			{
				doubleValueDecimalDigits = (doubleValueString.Length - (dotIndex + 1));
			}
			return doubleValueDecimalDigits;
		}
	}
}
