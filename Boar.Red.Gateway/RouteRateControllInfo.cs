using Boar.Red.Gateway.Extensions;
using System.Text.Json.Serialization;

namespace Boar.Red.Gateway
{
	/// <summary>
	/// 终点的路由流量比例控制信息。
	/// </summary>
	public class RouteRateControllInfo
	{
		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性

		public double DurationSeconds { get; set; }

		protected double _routeRate;

		protected int _routeRateDecimalDigits;

		public double RouteRate
		{
			get
			{
				return _routeRate;
			}
			set
			{
				_routeRate = value;
				_routeRateDecimalDigits = value.GetDecimalDigits();
			}
		}

		[JsonIgnore]
		public int RouteRateDecimalDigits => _routeRateDecimalDigits;

		#endregion
	}
}
