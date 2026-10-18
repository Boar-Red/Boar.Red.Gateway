using BaoXia.Utils;
using BaoXia.Utils.Extensions;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using System;
using System.Text.Json.Serialization;

namespace Boar.Red.Gateway
{
	public class EndPoint
	{
		////////////////////////////////////////////////
		// @自身属性
		////////////////////////////////////////////////

		#region 自身属性

		public string Name { get; set; } = string.Empty;

		public string PathTemplate { get; set; } = string.Empty;

		protected RouteRateControllInfo? _routeRateControllInfo;

		public RouteRateControllInfo DefaultRouteRateControllInfo
		{
			get
			{
				_routeRateControllInfo ??= new RouteRateControllInfo()
				{
					DurationSeconds = double.MaxValue,
					RouteRate = 1.0F
				};
				return _routeRateControllInfo;
			}
		}

		public double DefaultRouteRate
		{
			get
			{
				return this.DefaultRouteRateControllInfo.RouteRate;
			}
			set
			{
				this.DefaultRouteRateControllInfo.RouteRate = value;
			}
		}


		protected RouteRateControllInfo[]? _routeRateControllInfes;

		protected double _routeRateControlSeconds = 0.0;

		public RouteRateControllInfo[]? RouteRateControllInfes
		{
			get
			{
				return _routeRateControllInfes;
			}
			set
			{
				var routeRateControllInfes = value;
				var routeRateControlSeconds = 0.0;
				// 永远已”BegionSeconds“由小到大排序。
				if (routeRateControllInfes?.Length > 0)
				{
					foreach (var routeRateControllInfo in routeRateControllInfes)
					{
						routeRateControlSeconds += routeRateControllInfo.DurationSeconds;
					}
				}
				_routeRateControllInfes = routeRateControllInfes;
				_routeRateControlSeconds = routeRateControlSeconds;
			}
		}

		public double RouteRateControllSeconds => _routeRateControlSeconds;

		public DateTime RouteRateControllEndTime
		{
			get
			{
				return _stateChangedTime.AddSeconds(_routeRateControlSeconds);
			}
		}

		////////////////////////////////////////////////

		/// <summary>
		/// 每秒最大请求数，0为不限制。
		/// </summary>
		public double RPSMax { get; set; } = 0;

		/// <summary>
		/// 当前每秒请求数超过指定的最大值时，自动禁用。
		/// </summary>
		public bool IsDisableWhenRPSOverMax { get; set; }

		/// <summary>
		/// 当前每秒请求数超过指定的最大值时，返回的Http状态码。
		/// </summary>
		public int HttpStatusCodeWhenRPSOverMax { get; set; }

		[JsonIgnore]
		public bool IsDisableByRPSOverMax
		{
			get
			{
				if (this.RPSMax > 0
				    && this.IsDisableWhenRPSOverMax)
				{
					// !!! 注意这里一定是“>=”，“==rpsMax”表示当前终点满载 !!!
					if (this.RPS >= this.RPSMax
					    && DateTime.Now.EqualsInSecond(this.RPSStatisticsTime))
					{
						return true;
					}
				}
				return false;
			}
		}

		[JsonIgnore]
		public int RPSValue;


		/// <summary>
		/// 最近一秒中路由的请求数量，【重要】由统计系统标记。
		/// </summary>
		public int RPS
		{
			get
			{
				return RPSValue;
			}
			set
			{
				RPSValue = value;
			}
		}

		/// <summary>
		/// RPS所属时间。
		/// </summary>
		public DateTime RPSStatisticsTime { get; set; }


		////////////////////////////////////////////////

		public double ExceptionRateWatchingSeconds { get; set; }

		public int ExceptionRateWatchingRequestsCountMin { get; set; }

		public double ExceptionRateMax { get; set; }

		[JsonIgnore]
		public bool IsDisableByExceptionRateOverMax { get; set; }

		[JsonIgnore]
		public DateTime ExceptionRateOverMaxTime { get; set; }

		public double ExceptionRetryIntervalSeconds { get; set; }

		[JsonIgnore]
		public DateTime ExceptionRetryTime
		{
			get
			{
				var routerConfig = Config.Router;
				var endPointExceptionRetryIntervalSeconds
				    = this.ExceptionRetryIntervalSeconds > 0
				    ? this.ExceptionRetryIntervalSeconds
				    : routerConfig.EndPointExceptionRetryIntervalSeconds;
				if (endPointExceptionRetryIntervalSeconds <= 0)
				{
					return DateTime.MaxValue;
				}
				return this.ExceptionRateOverMaxTime.AddSeconds(endPointExceptionRetryIntervalSeconds);
			}
		}

		////////////////////////////////////////////////

		public EndPointState _state = EndPointState.Enable;
		public DateTime _stateChangedTime;

		[JsonIgnore]
		public EndPointState State
		{
			get
			{
				return _state;
			}
			set
			{
				if (_state == value)
				{
					return;
				}
				_state = value;
				_stateChangedTime = DateTime.Now;
			}
		}

		public String? StateName
		{
			get
			{
				return _state.Name();
			}

			set
			{
				this.State
				    = EnumUtil.ValueOf(
				    value,
				    EndPointState.Enable);
			}
		}

		////////////////////////////////////////////////

		[JsonIgnore]
		public bool IsConnectionEnable
		{
			get
			{
				if (this.IsConnectionEnableByConfigSetting != true)
				{
					return false;
				}
				if (this.IsDisableByRPSOverMax == true)
				{
					return false;
				}
				if (this.IsDisableByExceptionRateOverMax)
				{
					return false;
				}
				//
				return true;
			}

			set
			{
				if (value == true)
				{
					if (this.State == EndPointState.Disabling
					    || this.State == EndPointState.Disable)
					{
						this.State = EndPointState.Enabling;
					}
					this.IsDisableByExceptionRateOverMax = false;
				}
				else
				{
					if (this.State == EndPointState.Enabling
					    || this.State == EndPointState.Enable)
					{
						this.State = EndPointState.Disabling;
					}
				}
			}
		}

		[JsonIgnore]
		public bool IsConnectionEnableByConfigSetting
		{
			get
			{
				if (this.PathTemplate == null
				    || this.PathTemplate.Length < 1)
				{
					return false;
				}
				if (this.State == EndPointState.Disable)
				{
					return false;
				}
				if (this.DefaultRouteRate <= 0.0)
				{
					return false;
				}
				//
				return true;
			}
		}

		#endregion

		////////////////////////////////////////////////
		// @自身实现
		////////////////////////////////////////////////

		#region 自身实现

		public bool IsConnectionEnableForEndPointRoutePolicy(EndPointRoutePolicy endPointRoutePolicy)
		{
			if (endPointRoutePolicy == EndPointRoutePolicy.Manual)
			{
				return this.IsConnectionEnableByConfigSetting;
			}
			return this.IsConnectionEnable;
		}

		public bool IsInRouteRateControlling(DateTime routeTime)
		{
			if (// !!!⚠ 只有当前终点处于“可用中”状态时，才检查路由流量控制信息。 ⚠!!!
			    _state == EndPointState.Enabling
			    // !!!
			    && _routeRateControlSeconds > 0
			    && (routeTime - _stateChangedTime).TotalSeconds < _routeRateControlSeconds)
			{
				return true;
			}
			return false;
		}

		public RouteRateControllInfo? GetRouteRateConrollInfoSpecifiedAtRouteTime(
		    DateTime routeTime)
		{
			var routeRateControllInfes = _routeRateControllInfes;
			if (// !!!⚠ 只有当前终点处于“可用中”状态时，才检查路由流量控制信息。 ⚠!!!
			    _state == EndPointState.Enabling
			    // !!!
			    && routeRateControllInfes?.Length > 0
			    && _routeRateControlSeconds > 0)
			{
				var secondsAfterDisableStateChanged
				    = (routeTime - _stateChangedTime).TotalSeconds;
				if (secondsAfterDisableStateChanged < _routeRateControlSeconds)
				{
					var routeRateControllInfoEndSeconds = 0.0;
					foreach (var routeRateControllInfo in routeRateControllInfes)
					{
						routeRateControllInfoEndSeconds
						    += routeRateControllInfo.DurationSeconds;
						if (secondsAfterDisableStateChanged
						    < routeRateControllInfoEndSeconds)
						{
							System.Diagnostics.Debug.WriteLine(
							    "第 " + secondsAfterDisableStateChanged + "  秒，"
							    + "获取到流量控制为：" + routeRateControllInfo.RouteRate + " .");

							return routeRateControllInfo;
						}
					}
				}
			}
			return null;
		}

		public RouteRateControllInfo GetRouteRateConrollInfoAtRouteTime(
		    DateTime routeTime)
		{
			var routeRateControlInfoSpecified
			    = this.GetRouteRateConrollInfoSpecifiedAtRouteTime(routeTime);
			if (routeRateControlInfoSpecified != null)
			{
				return routeRateControlInfoSpecified;
			}
			return this.DefaultRouteRateControllInfo;
		}

		#endregion
	}
}
