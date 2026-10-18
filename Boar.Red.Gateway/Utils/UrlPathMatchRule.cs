using BaoXia.Utils;
using BaoXia.Utils.Extensions;
using Boar.Red.Gateway.Constants;
using System;

namespace Boar.Red.Gateway.Utils;

public class UrlPathMatchRule
{


	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	public string? CatalogPath { get; set; }

	protected string? _path;

	protected StringMatcher? _pathMatcher;

	public string? Path
	{
		get
		{
			return _path;
		}
		set
		{
			var path = value;
			if (path?.StartsWith(PathKeywords.RouteRuleCatalogPathPrefix) == true)
			{
				var catalogPathEndIndex = path.IndexOf(PathKeywords.PathSymbol_Backslash);
				if (catalogPathEndIndex < 0)
				{
					catalogPathEndIndex = path.Length;
				}
				// !!! 
				this.CatalogPath = path[PathKeywords.RouteRuleCatalogPathPrefix.Length..catalogPathEndIndex];
				// !!!
				if (catalogPathEndIndex >= path.Length - 1)
				{
					path = null;
				}
				else
				{
					path = path[(catalogPathEndIndex + 1)..];
				}
				// !!!
			}
			_path = path;

			_pathMatcher = new(path);
		}
	}


	////////////////////////////////////////////////
	// @类方法 
	////////////////////////////////////////////////

	#region 类方法

	public static UrlPathMatchRule? PathRuleFrom(string requestPath)
	{
		UrlPathMatchRule? pathRule = null;
		if (requestPath.Length > 0)
		{
			pathRule = new UrlPathMatchRule()
			{
				Path = requestPath
			};
		}
		return pathRule;
	}

	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public bool IsMatched(ClientRequestPathInfo? clientRequestPathInfo)
	{
		if (clientRequestPathInfo == null)
		{
			return false;
		}
		if (this.CatalogPath?.Length > 0
		    && this.CatalogPath.EqualsIgnoreCase(
		    clientRequestPathInfo.CatalogPath) != true)
		{
			return false;
		}

		var requestRelativePath = clientRequestPathInfo.RelativePath;
		if (_pathMatcher?.IsMatched(requestRelativePath,
		    StringComparison.OrdinalIgnoreCase,
		    true,
		    PathKeywords.RouteRequestPathSegment)
		    == true)
		{
			return true;
		}
		return false;
	}

	#endregion
}
