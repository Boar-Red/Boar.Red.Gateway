using System;
using System.Collections.Generic;

namespace Boar.Red.Gateway.Utils;

internal class ClientRequestRateLimiterInfo
{
	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	public Queue<DateTimeOffset> RequestTimes { get; } = [];

	public Dictionary<string, Queue<DateTimeOffset>> RequestTimesByTargetAPIPath { get; } = new(
		StringComparer.OrdinalIgnoreCase);

	#endregion
}
