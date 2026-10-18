using Boar.Red.Gateway.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Boar.Red.Gateway.Test.Utils;

[TestClass]
public class RouteRequestPathOptionsTest
{
	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	[TestMethod]
	[DataRow("@")]
	[DataRow("~")]
	[DataRow("gw-")]
	[DataRow("")]
	public void Parse_UsesConfiguredUrlPrefixAndKeepsRuleSyntaxAndQuery(string prefix)
	{
		var options = new RouteRequestPathOptions(prefix);
		using var path = RouteRule.CreateClientRequestPathInfoWithClientRequestPath("/" + prefix + "Stress_Test/get/section", "?key=%40&value=1", options);
		Assert.AreEqual("Stress_Test", path!.CatalogPath);
		Assert.AreEqual("get/section", path.RelativePath);
		Assert.AreEqual("?key=%40&value=1", path.QueryString);
		Assert.IsTrue(UrlPathMatchRule.PathRuleFrom("@Stress_Test/*")!.IsMatched(path));
		Assert.IsTrue(UrlPathMatchRule.PathRuleFrom("*")!.IsMatched(path));
		Assert.IsFalse(UrlPathMatchRule.PathRuleFrom("@Other_Test/*")!.IsMatched(path));
	}

	[TestMethod]
	[DataRow("@")]
	[DataRow("~")]
	[DataRow("gw-")]
	public void Parse_NonEmptyPrefixAllowsUnnamedRequests(string prefix)
	{
		using var path = RouteRule.CreateClientRequestPathInfoWithClientRequestPath("/" + prefix + "/get", null, new RouteRequestPathOptions(prefix));
		Assert.IsNull(path!.CatalogPath);
		Assert.AreEqual("get", path.RelativePath);
	}

	[TestMethod]
	public void Parse_EmptyPrefixRejectsReservedAndUnnamedPathsAndDoesNotAliasOldPrefix()
	{
		var options = new RouteRequestPathOptions("", ["Config", "RouteStatistics"]);
		Assert.IsNull(options.UnnamedRouteTemplate);
		foreach (var path in new[] { "/", "//get", "/CONFIG/get", "/routeStatistics/get", "/admin/workspace", "/assets/file.js" })
		{
			Assert.IsNull(RouteRule.CreateClientRequestPathInfoWithClientRequestPath(path, null, options), path);
		}
		using var oldPath = RouteRule.CreateClientRequestPathInfoWithClientRequestPath("/@Stress_Test/get", null, options);
		Assert.IsFalse(UrlPathMatchRule.PathRuleFrom("@Stress_Test/*")!.IsMatched(oldPath));
	}

	[TestMethod]
	[DataRow("/")]
	[DataRow("\\")]
	[DataRow(" ")]
	[DataRow("?")]
	[DataRow("#")]
	[DataRow("%40")]
	[DataRow("{catalog}")]
	[DataRow("*")]
	[DataRow(".")]
	[DataRow("中文")]
	public void Options_RejectUnsafePrefixes(string prefix)
	{
		Assert.Throws<ArgumentException>(() => new RouteRequestPathOptions(prefix));
	}

	[TestMethod]
	public void Options_RejectNullAndCopyReservedNames()
	{
		Assert.Throws<ArgumentException>(() => new RouteRequestPathOptions(null!));
		var names = new[] { "Config" };
		var options = new RouteRequestPathOptions("", names);
		names[0] = "Changed";
		Assert.IsTrue(options.IsReservedCatalogPath("config"));
		Assert.IsFalse(options.IsReservedCatalogPath("Changed"));
	}

	#endregion
}
