using BaoXia.Utils;
using BaoXia.Utils.Cache;
using BaoXia.Utils.Models;
using Boar.Red.Gateway.ConfigFiles;
using Boar.Red.Gateway.Constants;
using Boar.Red.Gateway.LogFiles;
using Boar.Red.Gateway.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Boar.Red.Gateway;

public class ClientResponseCaches
{
	////////////////////////////////////////////////
	// @静态常量
	////////////////////////////////////////////////

	private static HttpClient _httpClient = ClientResponseCaches.CreateHttpClientWithConfig(Config.Router);

	public static HttpClient HttpClient
	{
		get
		{
			return _httpClient;
		}
		set
		{
			_httpClient = value;
		}
	}

	public static HttpClient CreateHttpClientWithConfig(ConfigFiles.RouterConfig routerConfig)
	{
		var httpClient = new HttpClient(new HttpClientHandler()
		{
			// !!!⚠ 不存储Cookie，避免“包含”服务端状态 ⚠!!!
			UseCookies = false,
			// !!! 禁止自动跳转 !!!
			AllowAutoRedirect = false,
			// !!! 单点服务器并发最大数，交由路由控制 !!!
			MaxConnectionsPerServer = int.MaxValue
		})
		{
			Timeout = new TimeSpan(
		    0,
		    0,
		    (int)routerConfig.EndPointRequestTimeoutSecondsMax)
		};
		return httpClient;
	}

	public static void UpateCaches(ConfigFiles.RouterConfig routerConfig)
	{
		_httpClient = ClientResponseCaches.CreateHttpClientWithConfig(routerConfig);
	}

	////////////////////////////////////////////////
	// @自身属性
	////////////////////////////////////////////////

	#region 自身属性

	protected readonly ItemsCacheAsync<int, ClientResponseCache, ClientResponseCacheCreateParam> _clientResponseCaches;

	protected readonly ConcurrentBag<ClientResponseCacheAsyncCacheInfo> _clientResponseCachesNeedAsyncCache = [];
	protected readonly LoopTask _clientResponseCachesAsyncCacheTask;

	protected readonly ConcurrentBag<ClientResponseCache> _clientResponseCachesNeedToUpdate = [];
	protected readonly LoopTask _clientResponseCacheAutoUpdateTask;

	protected readonly ConcurrentBag<ClientResponseCache> _clientResponseCachesNeedToStorage = [];
	protected readonly LoopTask _clientResponseCacheAutoStorageTask;

	protected readonly CancellationTokenSource _cancellationTokenSource = new();

	public Func<string, CancellationToken, Task<ClientResponseCache?>>? ToDidGetResponseCacheFromStorage;

	public Func<List<ClientResponseCache>, CancellationToken, Task<ClientResponseCacheStorageInfo>>? ToDidStorageResponseCaches;

	public Func<CancellationToken, Task<int>>? ToDidCleanResponseCachesFromStorage;


	#endregion


	////////////////////////////////////////////////
	// @自身实现
	////////////////////////////////////////////////

	#region 自身实现

	public ClientResponseCaches()
	{
		_clientResponseCaches = new(async (requestHashCode, createParam) =>
		{
			// 无需缓存时，“requestHashCode”为“0”，因此要忽略此种情况。
			//if ( requestHashCode == 0 ||
			if (createParam == null)
			{
				return null;
			}

			var endPointRequestInfo = createParam.EndRequestInfo;
			if (endPointRequestInfo == null)
			{
				return null;
			}

			try
			{
				////////////////////////////////////////////////
				// 1/2，尝试从原始地址创建响应缓存：
				////////////////////////////////////////////////
				var clientResponseCache = await ClientResponseCache.CreateResponseCacheWithEndPointRequestInfoAsync(
					endPointRequestInfo, createParam.ConnectionIpEndPoints);

				////////////////////////////////////////////////
				// 2/2，使用原始数据创建缓存失败后，
				// 尝试从【持久化容器】中查询响应缓存：
				////////////////////////////////////////////////
				var toDidGetResponseCacheFromStorage = this.ToDidGetResponseCacheFromStorage;
				if (clientResponseCache == null
				&& endPointRequestInfo.EndPointRequestPersistedHashCode is string endPointRequestPersistedHashCode
				    && endPointRequestPersistedHashCode.Length > 0
				    && toDidGetResponseCacheFromStorage != null)
				{
					clientResponseCache = await toDidGetResponseCacheFromStorage(
						endPointRequestPersistedHashCode, _cancellationTokenSource.Token);
					clientResponseCache?.ResponseRouteResponseType = HttpHeaderValues.RouteResponseTypes.CacheDataFromStorage;
				}
				return clientResponseCache;
			}
			catch (Exception exception)
			{
				//
				Log.Exception.Logs(this, "更新响应缓存失败，程序异常。", exception);
				//
				return null;
			}
		},
		    null,
		    null,
		    () => Config.Router.ClientResponseCacheNoneReadSecondsToRelease,
		    () => Config.Router.ClientResponseCacheAutoUpdateIntervalSeconds,
		    null,
		    null)
		{
			// !!!
			IsNullValueValidToCache = false
			// !!!
		};


		////////////////////////////////////////////////
		////////////////////////////////////////////////
		////////////////////////////////////////////////


		_clientResponseCachesAsyncCacheTask = new(async (cancellationToken) =>
		{
			try
			{
				await this.DidTryToAsyncCacheClientResponsesAsync(cancellationToken);
			}
			catch
			{ }
			return true;
		},
		() => Config.Router.ClientResponseCacheAsyncCacheIntervalSeconds);

		_clientResponseCacheAutoUpdateTask = new(async (cancellationToken) =>
		    {
			    try
			    {
				    await this.DidTryToUpdateResponseCaches(cancellationToken);
			    }
			    catch
			    { }
			    return true;
		    },
		    () => Config.Router.ClientResponseCacheAutoUpdateIntervalSeconds);

		_clientResponseCacheAutoStorageTask = new(async (cancellationToken) =>
		    {
			    try
			    {
				    // 1/2，自动持久化缓存：
				    await DidTryToStorageResponseCaches(cancellationToken);

				    // 2/2，自动清理持久化缓存：
				    await DidTryToCleanResponseCachesStorage(cancellationToken);
			    }
			    catch
			    { }
			    return true;
		    },
		    () => Config.Router.ClientResponseCacheAutoStorageIntervalSeconds);
	}


	/// <summary>
	/// 尝试获取响应缓存。
	/// </summary>
	/// <param name="requestHashCode">来路请求对象的哈希码。</param>
	/// <param name="routeStatisticsInfoRecorder">路由统计信息记录器。</param>
	/// <returns>命中缓存时，返回：命中的缓存对象，否则返回：null。</returns>
	public async Task<ClientResponseCache?> ShootResponseCacheWithRequestInfoAsync(
	    EndPointRequestInfo endPointRequestInfo, ConnectionIpEndPoints? connectionIpEndPoints,
	    RouteStatisticsInfoRecorder routeStatisticsInfoRecorder)
	{
		ClientResponseCache? clientResponseCacheShot = null;
		int endPointRequestHashCodeNumber = 0;
		ClientResponseCacheCreateParam? clientResponseCacheCreateParam = null;
		var isClientResponseNeedAsyncCache = false;
		var isClientResponseGotFromCaches = false;
		if (endPointRequestInfo.IsNeedReturnCacheFirst)
		{
			////////////////////////////////////////////////
			// !!! ⚠⚠⚠
			endPointRequestHashCodeNumber
			    = endPointRequestInfo.TryUpdateAndGetEndPointRequestHashCodeNumber();
			// !!! ⚠⚠⚠
			////////////////////////////////////////////////

			clientResponseCacheCreateParam
			    = new ClientResponseCacheCreateParam(endPointRequestInfo, connectionIpEndPoints);
			var clientResponseCacheContainerShot
			    = await _clientResponseCaches.GetItemContainerAsync(
			    endPointRequestHashCodeNumber,
			    clientResponseCacheCreateParam);
			// !!!
			clientResponseCacheShot = clientResponseCacheContainerShot?.Item;
			isClientResponseGotFromCaches = true;
			// !!!
			if (clientResponseCacheContainerShot != null
			    && clientResponseCacheShot != null)
			{
				// #路由统计#
				var isResponseGetFromEndPoint = false;
				if (clientResponseCacheShot.ResponseRouteResponseType
				    == HttpHeaderValues.RouteResponseTypes.OriginalData
				    && clientResponseCacheContainerShot.ItemCreateParam == clientResponseCacheCreateParam)
				{
					isResponseGetFromEndPoint = true;
				}
				// !!!
				routeStatisticsInfoRecorder.IsResponseGetFromEndPoint = isResponseGetFromEndPoint;
				// !!!
				if (!isResponseGetFromEndPoint)
				{
					routeStatisticsInfoRecorder.EndFindResponseCache();
				}
			}
		}
		else
		{
			if (endPointRequestInfo.IsNeedSaveResponseCache == true)
			{
				isClientResponseNeedAsyncCache = true;
			}

			var toDidCreateItemCacheAsync = _clientResponseCaches.ToDidCreateItemCacheAsync;
			if (toDidCreateItemCacheAsync != null)
			{
				clientResponseCacheCreateParam = new ClientResponseCacheCreateParam(endPointRequestInfo, connectionIpEndPoints);
				// !!!⚠ 注意这里“toDidCreateItemCacheAsync”，本身就包含了网络获取和“缓存”获取 ⚠!!!
				clientResponseCacheShot = await toDidCreateItemCacheAsync(
				    // !!! 无需获取缓存时，则不再计算，使用终点请求的哈希值 !!!
				    0,
				    clientResponseCacheCreateParam);
			}
			// !!!
			routeStatisticsInfoRecorder.IsResponseGetFromEndPoint = true;
			// !!!
		}

		// !!! 命中缓存后，更新缓存信息 !!!
		if (clientResponseCacheShot != null)
		{
			var validHttpStatusCodesToCache = Config.Router.EndPointResponseCacheValidHttpStatusCodes;
			if (validHttpStatusCodesToCache?.Contains(
			    clientResponseCacheShot.ResponseHttpStatusCode)
			    != true)
			{
				if (isClientResponseGotFromCaches
				    && endPointRequestHashCodeNumber != 0)
				{
					// !!!
					await _clientResponseCaches.RemoveAsync(endPointRequestHashCodeNumber);
					// !!!
				}

				var clientResponseCacheExceptionTitle
				    = ExceptionMessages
				    .RequestToEndPointFailed_InvalidHttpStatusCodes
				    .Replace(
				    "#终点Url#",
				    clientResponseCacheShot.EndPointRequestUrl)
				    .Replace(
				    "#状态码#",
				    clientResponseCacheShot.ResponseHttpStatusCode.ToString());
				var clientResponseCacheException
				    = new System.Net.Http.HttpRequestException(
				    clientResponseCacheExceptionTitle,
				    null,
				    (System.Net.HttpStatusCode?)clientResponseCacheShot.ResponseHttpStatusCode);
				// !!! ⚠
				throw clientResponseCacheException;
				// !!! ⚠
			}


			////////////////////////////////////////////////
			clientResponseCacheShot.UpdateByRequestShotForRequestInfo(
			    endPointRequestInfo,
			    clientResponseCacheCreateParam,
			    isClientResponseNeedAsyncCache,
			    in _clientResponseCachesNeedAsyncCache,
			    in _clientResponseCachesNeedToUpdate,
			    in _clientResponseCachesNeedToStorage);
			////////////////////////////////////////////////

		}
		return clientResponseCacheShot;
	}

	/// <summary>
	/// 根据终点请求信息，尝试获取响应缓存。
	/// </summary>
	/// <param name="endPointRequestInfo">根据终点请求信息。</param>
	/// <returns>对应的响应缓存。</returns>
	public async Task<ClientResponseCache?> TryGetResponseCacheWithRequestInfoAsync(
	    EndPointRequestInfo endPointRequestInfo)
	{
		var endPointRequestHashCodeNumber
		    = endPointRequestInfo.TryUpdateAndGetEndPointRequestHashCodeNumber();
		if (endPointRequestHashCodeNumber == 0)
		{
			return null;
		}

		var clientResponseCacheItem
		    = await _clientResponseCaches.TryGetAsync(endPointRequestHashCodeNumber);
		if (clientResponseCacheItem != null)
		{
			return clientResponseCacheItem.Item;
		}
		return null;
	}


	/// <summary>
	/// 为给定的请求哈希码增加响应缓存。
	/// </summary>
	/// <param name="requestHashCode">给定的请求哈希码。</param>
	/// <param name="clientResponseCache">响应缓存对象。</param>
	public async Task AddResponseCacheAsync(ClientResponseCache clientResponseCache)
	{
		if (clientResponseCache.EndPointRequestRunningHashCode
		    == 0)
		{
			return;
		}
		// !!!
		clientResponseCache.LastUpdateTime = DateTime.Now;
		// !!!
		await _clientResponseCaches.AddAsync(
		    clientResponseCache.EndPointRequestRunningHashCode,
		    clientResponseCache);
	}

	public int GetAllResponseCachesCount()
	{
		return _clientResponseCaches.Count;
	}

	public async Task<int> RemoveResponseCachesWithRequestRelativePathAsync(string requestRelativePath)
	{
		if (requestRelativePath == null
		    || requestRelativePath.Length < 1)
		{
			return 0;
		}
		var allClientResponseCacheContainers = _clientResponseCaches.ValueContainers;
		if (allClientResponseCacheContainers == null
		    || allClientResponseCacheContainers.Count < 1)
		{
			return 0;
		}

		var requestRelativePathMatcher = new StringMatcher(requestRelativePath);

		var clientResponseCachesCountRemoved = 0;
		foreach (var clientResponseCacheContainer in allClientResponseCacheContainers)
		{
			if (clientResponseCacheContainer.Item is ClientResponseCache clientResponseCache)
			{
				var clientRequestPath = clientResponseCache.RequestPath;
				if (requestRelativePathMatcher.IsMatched(clientRequestPath) == true)
				{
					if (clientResponseCache.EndPointRequestRunningHashCode != 0)
					{
						var clientResponseCacheRemoved
						    = await _clientResponseCaches.RemoveAsync(
						    clientResponseCache.EndPointRequestRunningHashCode);
						if (clientResponseCacheRemoved != null)
						{
							clientResponseCachesCountRemoved++;
						}
					}
				}
			}
		}
		return clientResponseCachesCountRemoved;
	}

	public int Clear()
	{
		var responseCachesRemoved = _clientResponseCaches.Count;
		{
			_clientResponseCaches.Clear();
		}
		return responseCachesRemoved;
	}

	#endregion


	////////////////////////////////////////////////
	// @事件节点
	////////////////////////////////////////////////

	#region 事件节点

	/// <summary>
	/// 异步缓存客户端响应。
	/// </summary>
	protected async Task DidTryToAsyncCacheClientResponsesAsync(CancellationToken cancellationToken)
	{
		if (_clientResponseCachesNeedAsyncCache.IsEmpty)
		{
			return;
		}

		int clientResponseCachesCountAsyncCached = 0;
		while (cancellationToken.IsCancellationRequested == false
		    && _clientResponseCachesNeedAsyncCache.TryTake(out var clientResponseCacheAsyncCacheInfo))
		{
			if (clientResponseCacheAsyncCacheInfo == null)
			{
				continue;
			}
			var clientResponseCache
			    = clientResponseCacheAsyncCacheInfo.ClientResponseCache;
			if (clientResponseCache == null)
			{
				continue;
			}

			var clientResponseCacheEndPointRequestRunningHashCode
			    = clientResponseCache.TryUpdateAndGetEndPointRequestHashCodeNumber();
			if (clientResponseCacheEndPointRequestRunningHashCode == 0)
			{
				continue;
			}

			var clientResponseCacheCreateParam
			    = clientResponseCacheAsyncCacheInfo.ClientResponseCacheCreateParam;

			await _clientResponseCaches.AddAsync(
			    clientResponseCacheEndPointRequestRunningHashCode,
			    clientResponseCache,
			    clientResponseCacheCreateParam);
			//
			// !!!
		}

		Log.Logs.Logs(this,
		    "异步缓存了响应" + clientResponseCachesCountAsyncCached + " 条，"
		    + "当前缓存共 " + _clientResponseCaches.Count + " 条。");
	}

	/// <summary>
	/// 更新响应缓存。
	/// </summary>
	protected async Task DidTryToUpdateResponseCaches(CancellationToken cancellationToken)
	{
		if (_clientResponseCachesNeedToUpdate.IsEmpty)
		{
			return;
		}

		var clientResponseCachesCountUpdated = 0;
		var clientResponseCachesCountUpdatedCompleted = 0;
		var clientResponseCachesCountUpdatedFailed = 0;
		while (cancellationToken.IsCancellationRequested == false
		    && _clientResponseCachesNeedToUpdate.TryTake(out var clientResponseCacheNeedUpdate))
		{
			if (clientResponseCacheNeedUpdate.IsNeedToUpdate != true)
			{
				continue;
			}

			try
			{
				clientResponseCachesCountUpdated++;
				if (clientResponseCachesCountUpdated < Config.Router.ClientResponseCacheAutoUpdateThreadsCount)
				{
					await ClientResponseCache.UpdateResponseCacheAsync(clientResponseCacheNeedUpdate, null);
					// !!!
					clientResponseCacheNeedUpdate.ResponseRouteResponseType
					    = HttpHeaderValues.RouteResponseTypes.CacheData;
					// !!!
					clientResponseCachesCountUpdatedCompleted++;
					// !!!
				}
				else
				{
					break;
				}
			}
			catch (Exception exception)
			{
				Log.Exception.Logs(this, "定时更新响应缓存失败，程序异常。", exception);
				// !!!
				clientResponseCachesCountUpdatedFailed++;
				// !!!
			}
			finally
			{
				// !!!
				clientResponseCacheNeedUpdate.IsInUpdateQueue = false;
				clientResponseCacheNeedUpdate.LastUpdateTime = DateTime.Now;
				// !!!
			}
		}
		Log.Logs.Logs(this,
		    "更新响应缓存 " + clientResponseCachesCountUpdated + " 条，"
		    + "成功 " + clientResponseCachesCountUpdatedCompleted + " 条，"
		    + "失败 " + clientResponseCachesCountUpdatedFailed + " 条。");
	}

	/// <summary>
	/// 尝试【存储】响应缓存。
	/// </summary>
	protected async Task DidTryToStorageResponseCaches(CancellationToken cancellationToken)
	{
		var toDidStorageResponseCaches
		    = this.ToDidStorageResponseCaches;
		if (toDidStorageResponseCaches == null)
		{
			// !!! ⚠ 没有指定存储方法时，要直接清理掉存储队列。 !!!
			_clientResponseCachesNeedToStorage.Clear();
			// !!! ⚠
			return;
		}

		var now = DateTime.Now;
		var clientResponseCachesNeedToStorage = new List<ClientResponseCache>();
		while (cancellationToken.IsCancellationRequested == false
		    && _clientResponseCachesNeedToStorage.TryTake(out var clientResponseCacheNeedToStorage))
		{
			if (clientResponseCacheNeedToStorage.IsNeedToStorage != true)
			{
				continue;
			}

			// !!!
			clientResponseCacheNeedToStorage.TryUpdateAndGetEndPointRequestHashCodeNumber();
			// !!!
			clientResponseCacheNeedToStorage.StorageTime = now;
			clientResponseCacheNeedToStorage.IsInStorageQueue = false;
			// !!!
			clientResponseCachesNeedToStorage.Add(clientResponseCacheNeedToStorage);
			if (clientResponseCachesNeedToStorage.Count
			    >= Config.Router.ClientResponseCachesCountToAutoStorage)
			{
				break;
			}
		}
		if (clientResponseCachesNeedToStorage.Count < 1)
		{
			return;
		}
		try
		{
			var clientResponseCacheStorageInfo
			    = await toDidStorageResponseCaches(
			    clientResponseCachesNeedToStorage,
			    _cancellationTokenSource.Token);


			Log.Logs.Logs(this,
			    "存储响应缓存成功，"
			    + "新增 " + clientResponseCacheStorageInfo?.CountCreated + " 条，"
			    + "更新 " + clientResponseCacheStorageInfo?.CountUpdated + " 条。");

		}
		catch (Exception exception)
		{
			Log.Exception.Logs(this, "存储响应缓存失败，程序异常。", exception);
		}
	}

	/// <summary>
	/// 尝试【清理】响应缓存存储。
	/// </summary>
	protected async Task DidTryToCleanResponseCachesStorage(CancellationToken cancellationToken)
	{
		try
		{
			var toDidCleanResponseCachesFromStorage
			    = this.ToDidCleanResponseCachesFromStorage;
			if (toDidCleanResponseCachesFromStorage != null)
			{
				var clientResponseCachesCountRemoved = await toDidCleanResponseCachesFromStorage(cancellationToken);
				//
				Log.Logs.Logs(this, "清理响应缓存成功 " + clientResponseCachesCountRemoved + " 条。");
				//
			}
		}
		catch (Exception exception)
		{
			Log.Exception.Logs(this, "清理响应缓存失败，程序异常。", exception);
		}
	}

	#endregion
}
