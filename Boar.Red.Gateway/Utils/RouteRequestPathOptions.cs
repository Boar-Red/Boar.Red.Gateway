using Boar.Red.Gateway.Constants;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Boar.Red.Gateway.Utils;

/// <summary>启动时确定的路由 URL 语法，不随配置文件热更新。</summary>
public sealed class RouteRequestPathOptions
{
	////////////////////////////////////////////////
	// @静态变量
	////////////////////////////////////////////////

	#region 静态变量

	public static RouteRequestPathOptions Default { get; } = new(PathKeywords.RouteRequestPathSpliter);

	#endregion

	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	private readonly FrozenSet<string> _reservedCatalogPaths;

	public string PathSpliter { get; }
	public string PathSpliterPrefix { get; }
	public string NamedRouteTemplate { get; }
	public string? UnnamedRouteTemplate { get; }

	#endregion

	////////////////////////////////////////////////
	// @类方法
	////////////////////////////////////////////////

	#region 类方法

	public static void ValidatePathSpliter(string? pathSpliter)
	{
		if (pathSpliter == null || pathSpliter.Any(character => !char.IsAsciiLetterOrDigit(character) && "@~!$_-".IndexOf(character) < 0))
		{
			throw new ArgumentException("路由 URL 前缀只支持英文字母、数字和 @ ~ ! $ _ -；空字符串表示不使用前缀。", nameof(pathSpliter));
		}
	}

	#endregion

	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public RouteRequestPathOptions(string pathSpliter, IEnumerable<string>? reservedCatalogPaths = null)
	{
		ValidatePathSpliter(pathSpliter);
		PathSpliter = pathSpliter;
		PathSpliterPrefix = "/" + pathSpliter;
		NamedRouteTemplate = PathSpliterPrefix + "{" + PathKeywords.SystemRouteValueKey_RouteRequestCatalogPath + "}/{*" + PathKeywords.SystemRouteValueKey_RouteRequestPath + "}";
		UnnamedRouteTemplate = pathSpliter.Length > 0 ? PathSpliterPrefix + "/{*" + PathKeywords.SystemRouteValueKey_RouteRequestPath + "}" : null;
		// 管理页面和前端资源目录也是网关内部地址。
		_reservedCatalogPaths = (reservedCatalogPaths ?? []).Concat(["admin", "assets"]).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
	}

	public bool IsReservedCatalogPath(string catalogPath)
	{
		return PathSpliter.Length == 0 && _reservedCatalogPaths.Contains(catalogPath);
	}

	#endregion
}
