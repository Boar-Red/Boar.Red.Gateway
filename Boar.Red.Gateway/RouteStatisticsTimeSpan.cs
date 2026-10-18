using Microsoft.EntityFrameworkCore;

namespace Boar.Red.Gateway
{
	[Owned]
	public class RouteStatisticsTimeSpan
	{

		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性

		public float TotalMilliseconds { get; set; }

		public int Times { get; set; }

		public float AvgMilliseconds
		{
			get
			{
				if (this.Times > 0)
				{
					return this.TotalMilliseconds / this.Times;
				}
				return 0;
			}
		}

		public float MinMilliseconds { get; set; } = float.MaxValue;

		public float MaxMilliseconds { get; set; } = float.MinValue;


		#endregion


		////////////////////////////////////////////////
		// @自身实现
		////////////////////////////////////////////////

		#region 自身实现

		public RouteStatisticsTimeSpan AddMilliseconds(float milliseconds)
		{
			if (milliseconds < 0.0F)
			{
				return this;
			}

			this.TotalMilliseconds += milliseconds;
			this.Times++;

			if (this.MinMilliseconds > milliseconds)
			{
				this.MinMilliseconds = milliseconds;
			}
			if (this.MaxMilliseconds < milliseconds)
			{
				this.MaxMilliseconds = milliseconds;
			}

			return this;
		}

		public RouteStatisticsTimeSpan AddMilliseconds(long milliseconds)
		{
			return this.AddMilliseconds((float)milliseconds);
		}

		public RouteStatisticsTimeSpan AddStatisticsTimeSpan(RouteStatisticsTimeSpan timeSpan)
		{
			if (timeSpan == null)
			{
				return this;
			}

			this.TotalMilliseconds += timeSpan.TotalMilliseconds;
			this.Times += timeSpan.Times;

			if (this.MinMilliseconds > timeSpan.MinMilliseconds)
			{
				this.MinMilliseconds = timeSpan.MinMilliseconds;
			}
			if (this.MaxMilliseconds < timeSpan.MaxMilliseconds)
			{
				this.MaxMilliseconds = timeSpan.MaxMilliseconds;
			}

			return this;
		}

		#endregion
	}
}
