using BaoXia.Utils.Models;
using Boar.Red.Gateway.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway.Test.Utils;

[TestClass]
public class ClientRequestRateLimiterTest
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[TestMethod]
	public void IsRequestAllowed_WhenAllLimitsDisabled_ReturnsTrue()
	{
		var requestRateLimiter = new ClientRequestRateLimiter(
			new ClientRequestRateLimiterOptions());

		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/orders"));
		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/orders"));
	}

	[TestMethod]
	public void IsClientRequestAllowed_WhenRouteRuleHasOwnLimiterOptions_LimitsOnlyThatRouteRule()
	{
		var routeRuleA = new RouteRule()
		{
			ClientRequestRateLimiterOptions = new ClientRequestRateLimiterOptions()
			{
				IsRequestSourceIPLimitEnable = true,
				RequestSourceIPLimitSamplingSeconds = 10.0,
				RequestSourceIPLimitRequestsCountMax = 1
			}
		};
		var routeRuleB = new RouteRule()
		{
			ClientRequestRateLimiterOptions = new ClientRequestRateLimiterOptions()
			{
				IsRequestSourceIPLimitEnable = true,
				RequestSourceIPLimitSamplingSeconds = 10.0,
				RequestSourceIPLimitRequestsCountMax = 1
			}
		};

		Assert.IsNotNull(routeRuleA.ClientRequestRateLimiter);

		// 与生产构造路径一致：无网关/代理头时，客户端地址取 TcpIpRemoteIpEndPoint（KeyIpEndPoint 专用槽位）。
		// ConnectionIpEndPoints.TryParse 仅填充通用 IpEndPoints 列表，不会填充 KeyIpEndPoint 专用槽位，
		// RouteRule.IsClientRequestAllowed 基于 KeyIpEndPointAddress 判断，会因取不到地址而直接拒绝。
		var targetConnectionIpEndPoints = new ConnectionIpEndPoints(
			null,
			null,
			null,
			null,
			new IPEndPoint(IPAddress.Parse("192.168.1.10"), 0));

		Assert.IsTrue(routeRuleA.IsClientRequestAllowed(targetConnectionIpEndPoints, "/api/orders"));

		Assert.IsFalse(routeRuleA.IsClientRequestAllowed(targetConnectionIpEndPoints, "/api/orders"));

		Assert.IsTrue(routeRuleB.IsClientRequestAllowed(targetConnectionIpEndPoints, "/api/orders"));
	}

	[TestMethod]
	public void IsRequestAllowed_WhenRequestSourceIPLimitExceeded_ReturnsFalseUntilWindowExpires()
	{
		var timeProvider = CreateTimeProvider();
		var requestRateLimiter = new ClientRequestRateLimiter(
			new ClientRequestRateLimiterOptions()
			{
				IsRequestSourceIPLimitEnable = true,
				RequestSourceIPLimitSamplingSeconds = 10.0,
				RequestSourceIPLimitRequestsCountMax = 2
			},
			timeProvider);

		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/orders"));
		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/users"));
		Assert.IsFalse(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/payments"));
		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.11",
			"/api/payments"));

		timeProvider.Advance(TimeSpan.FromSeconds(10.0));

		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/payments"));
	}

	[TestMethod]
	public void IsRequestAllowed_WhenRequestSourceIPAndTargetAPIPathLimitExceeded_ReturnsFalseOnlyForThatPath()
	{
		var requestRateLimiter = new ClientRequestRateLimiter(
			new ClientRequestRateLimiterOptions()
			{
				IsRequestSourceIPAndTargetAPIPathLimitEnable = true,
				RequestSourceIPAndTargetAPIPathLimitSamplingSeconds = 10.0,
				RequestSourceIPAndTargetAPIPathLimitRequestsCountMax = 2
			});

		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/orders?currentPage=1"));
		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/API/ORDERS?currentPage=2"));
		Assert.IsFalse(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/orders"));
		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/users"));
		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.11",
			"/api/orders"));
	}

	[TestMethod]
	public void IsRequestAllowed_WhenBothLimitsEnabled_AppliesBothLimitsAtomically()
	{
		var requestRateLimiter = new ClientRequestRateLimiter(
			new ClientRequestRateLimiterOptions()
			{
				IsRequestSourceIPLimitEnable = true,
				RequestSourceIPLimitSamplingSeconds = 10.0,
				RequestSourceIPLimitRequestsCountMax = 3,
				IsRequestSourceIPAndTargetAPIPathLimitEnable = true,
				RequestSourceIPAndTargetAPIPathLimitSamplingSeconds = 10.0,
				RequestSourceIPAndTargetAPIPathLimitRequestsCountMax = 2
			});

		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/orders"));
		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/orders"));
		Assert.IsFalse(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/orders"));
		Assert.IsTrue(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/users"));
		Assert.IsFalse(requestRateLimiter.IsRequestAllowed(
			"192.168.1.10",
			"/api/payments"));
	}

	[TestMethod]
	public void IsRequestAllowed_WhenConcurrentRequestsExceedLimit_OnlyMaximumRequestsAreAllowed()
	{
		const int requestsCountMax = 100;
		var requestRateLimiter = new ClientRequestRateLimiter(
			new ClientRequestRateLimiterOptions()
			{
				IsRequestSourceIPLimitEnable = true,
				RequestSourceIPLimitSamplingSeconds = 10.0,
				RequestSourceIPLimitRequestsCountMax = requestsCountMax
			});
		var requestsAllowedCount = 0;

		Parallel.For(0, requestsCountMax * 2, _ =>
		{
			if (requestRateLimiter.IsRequestAllowed(
				"192.168.1.10",
				"/api/orders"))
			{
				Interlocked.Increment(ref requestsAllowedCount);
			}
		});

		Assert.AreEqual(requestsCountMax, requestsAllowedCount);
	}

	#endregion


	////////////////////////////////////////////////
	// @私有方法
	////////////////////////////////////////////////

	#region 私有方法

	static ClientRequestRateLimiterTestTimeProvider CreateTimeProvider()
	{
		return new ClientRequestRateLimiterTestTimeProvider(
			new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	}

	#endregion
}
