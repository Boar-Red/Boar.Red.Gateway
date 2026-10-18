using BaoXia.Utils.Extensions;
using System;

namespace Boar.Red.Gateway.ViewModels
{
	public class EndPointStateInfo
	{

		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性


		public string? Name { get; set; }

		public string? StateName { get; set; }

		public bool IsEnable { get; set; }

		public bool IsDisableByExceptionRateOverMax { get; set; }

		public long ExceptionRetryTimeStamp { get; set; }

		#endregion

		////////////////////////////////////////////////
		// @自身实现
		////////////////////////////////////////////////

		#region 自身实现

		public EndPointStateInfo()
		{ }

		public EndPointStateInfo(
		    string name,
		    bool isEnable,
		    bool isDisableByExceptionRateOverMax = false,
		    DateTime exceptionRetryTime = default)
		{
			this.Name = name;
			this.StateName = isEnable
			    ? EndPointState.Enable.Name()
			    : EndPointState.Disable.Name();
			this.IsEnable = isEnable;
			this.IsDisableByExceptionRateOverMax = isDisableByExceptionRateOverMax;
			var exceptionRetryTimeStamp = exceptionRetryTime.MillisecondsFrom1970();
			if (exceptionRetryTimeStamp < 0)
			{
				exceptionRetryTimeStamp = 0;
			}
			this.ExceptionRetryTimeStamp = exceptionRetryTimeStamp;
		}

		public EndPointStateInfo(
		    EndPoint endPoint)
		{
			this.Name = endPoint.Name;
			this.StateName = endPoint.State.Name();
			this.IsEnable = endPoint.IsConnectionEnable;
			this.IsDisableByExceptionRateOverMax = endPoint.IsDisableByExceptionRateOverMax;
			this.ExceptionRetryTimeStamp = endPoint.ExceptionRetryTime.MillisecondsFrom1970();
		}

		#endregion


		////////////////////////////////////////////////
		// @重载
		////////////////////////////////////////////////

		#region 重载

		public override bool Equals(object? obj)
		{
			if (obj is EndPointStateInfo anotherEndPointStateInfo
			    && this.Name == anotherEndPointStateInfo.Name)
			{
				return true;
			}
			return false;
		}

		public override int GetHashCode()
		{
			var name = this.Name;
			if (name != null)
			{
				return name.GetHashCode();
			}
			return 0;
		}

		#endregion

	}
}
