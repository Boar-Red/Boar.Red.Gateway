using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Boar.Red.Gateway.Utils;

public static class ConfigDocumentUtil
{
	////////////////////////////////////////////////
	// @静态变量
	////////////////////////////////////////////////

	#region 静态变量

	public static readonly JsonSerializerOptions JsonOptions = new(BaoXia.Utils.Environment.JsonSerializerOptions ?? new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		AllowTrailingCommas = true,
		ReadCommentHandling = JsonCommentHandling.Skip
	})
	{
		WriteIndented = true
	};

	#endregion

	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public static JsonObject Parse(string json)
	{
		return JsonNode.Parse(json, new JsonNodeOptions { PropertyNameCaseInsensitive = true },
			new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject
			?? throw new ArgumentException("配置文件必须是 JSON 对象。");
	}

	public static JsonObject ToDocument(object value)
	{
		return Parse(JsonSerializer.Serialize(value, JsonOptions));
	}

	public static T? Get<T>(JsonObject document, string name)
	{
		return document[name] == null ? default : document[name]!.Deserialize<T>(JsonOptions);
	}

	public static JsonObject Section<T>(JsonObject document, string name) where T : new()
	{
		if (document[name] is JsonObject section)
		{
			return section;
		}
		var newSection = ToDocument(new T());
		document[name] = newSection;
		return newSection;
	}

	public static void Set<T>(JsonObject document, string name, T value)
	{
		document[name] = JsonSerializer.SerializeToNode(value, JsonOptions);
	}

	public static string[] SplitValues(string? value)
	{
		return (value ?? string.Empty).Split([',', '，', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	public static string JoinValues(string[]? values)
	{
		return string.Join(",", values ?? []);
	}

	public static void ValidateBindAddress(string? address)
	{
		Require(string.IsNullOrWhiteSpace(address) || address.Trim() == "*" || IPAddress.TryParse(address.Trim(), out _), "服务器绑定地址必须是有效 IP 地址或 *。");
	}

	public static bool IsIpAddressOrPattern(string value)
	{
		if (IPAddress.TryParse(value, out _))
		{
			return true;
		}
		if (value == "*")
		{
			return true;
		}
		var parts = value.Split('.');
		return parts.Length == 4 && parts.All(part => part == "*" || byte.TryParse(part, out _));
	}

	public static void Require(bool condition, string description)
	{
		if (!condition)
		{
			throw new ArgumentException(description);
		}
	}

	public static void RequireNonNegative(double value, string name)
	{
		Require(double.IsFinite(value) && value >= 0, name + "必须为有限的非负数。");
	}

	#endregion
}
