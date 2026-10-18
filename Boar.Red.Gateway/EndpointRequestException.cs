using System;

namespace Boar.Red.Gateway
{
	public class EndPointException
	{

		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性

		public DateTime Time { get; set; }

		public Exception? Exception { get; set; }

		#endregion


		////////////////////////////////////////////////
		// @自身实现
		////////////////////////////////////////////////

		#region 自身实现

		public EndPointException()
		{ }

		public EndPointException(
		    DateTime time,
		    Exception exception)
		{
			this.Time = time;
			this.Exception = exception;
		}

		public EndPointException(
		    Exception exception)
		{
			this.Time = DateTime.Now;
			this.Exception = exception;
		}

		#endregion
	}
}
