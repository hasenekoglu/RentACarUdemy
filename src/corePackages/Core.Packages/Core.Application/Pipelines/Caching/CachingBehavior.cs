using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace Core.Application.Pipelines.Caching;

    public class CachingBehavior<TRequest,TResponse>: IPipelineBehavior<TRequest,TResponse>
    where TRequest : ICachableRequest, IRequest<TResponse>
    {
        private readonly CacheSetting _cacheSetting;
        private readonly IDistributedCache _cache;
        private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

        public CachingBehavior(IDistributedCache cache,  IConfiguration configuration, ILogger<CachingBehavior<TRequest, TResponse>> logger)
        {
            _cacheSetting = configuration.GetSection("CacheSettings").Get<CacheSetting>()?? throw new InvalidOperationException();
            _cache = cache;
            _logger = logger;
        }

    
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (request.BypassCache)
            {
                return await next();
            }

            TResponse response;
            byte[]? cachedResponse = await _cache.GetAsync(request.Cachekey, cancellationToken);
            if (cachedResponse != null)
            {
                response = JsonSerializer.Deserialize<TResponse>(Encoding.Default.GetString(cachedResponse));
                _logger.LogInformation($"Fetched from cache -> {request.Cachekey}");
            }
            else
            {
                response = await getResponseAndAddToCashe(request,next,cancellationToken);
            }

            return response;
        }

        private async Task<TResponse> getResponseAndAddToCashe(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)

        {
            TResponse response = await next();
            TimeSpan slidingExpiration = request.SlidingExpiration ?? TimeSpan.FromDays(_cacheSetting.SlidingExpiration);
            DistributedCacheEntryOptions cacheOptions = new() { SlidingExpiration = slidingExpiration };
            
            byte[] serializedData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));

            await _cache.SetAsync(request.Cachekey, serializedData, cacheOptions, cancellationToken);
            _logger.LogInformation($"Added to cache -> {request.Cachekey}");

            if (request.CacheGroupKey is not null)
                await addCacheKeyToGroup(request, slidingExpiration, cancellationToken);


            return response;

        }

       private async Task addCacheKeyToGroup(TRequest request, TimeSpan slidingExpiration, CancellationToken cancellationToken)
    {
        byte[]? cacheGroupCache = await _cache.GetAsync(key: request.CacheGroupKey!, cancellationToken);
        HashSet<string> cacheKeysInGroup;
        if (cacheGroupCache != null)
        {
            cacheKeysInGroup = JsonSerializer.Deserialize<HashSet<string>>(Encoding.Default.GetString(cacheGroupCache))!;
            if (!cacheKeysInGroup.Contains(request.Cachekey))
                cacheKeysInGroup.Add(request.Cachekey);
        }
        else
            cacheKeysInGroup = new HashSet<string>(new[] { request.Cachekey });
        byte[] newCacheGroupCache = JsonSerializer.SerializeToUtf8Bytes(cacheKeysInGroup);

        byte[]? cacheGroupCacheSlidingExpirationCache = await _cache.GetAsync(
            key: $"{request.CacheGroupKey}SlidingExpiration",
            cancellationToken
        );
        int? cacheGroupCacheSlidingExpirationValue = null;
        if (cacheGroupCacheSlidingExpirationCache != null)
            cacheGroupCacheSlidingExpirationValue = Convert.ToInt32(Encoding.Default.GetString(cacheGroupCacheSlidingExpirationCache));
        if (cacheGroupCacheSlidingExpirationValue == null || slidingExpiration.TotalSeconds > cacheGroupCacheSlidingExpirationValue)
            cacheGroupCacheSlidingExpirationValue = Convert.ToInt32(slidingExpiration.TotalSeconds);
        byte[] serializeCachedGroupSlidingExpirationData = JsonSerializer.SerializeToUtf8Bytes(cacheGroupCacheSlidingExpirationValue);

        DistributedCacheEntryOptions cacheOptions =
            new() { SlidingExpiration = TimeSpan.FromSeconds(Convert.ToDouble(cacheGroupCacheSlidingExpirationValue)) };

        await _cache.SetAsync(key: request.CacheGroupKey!, newCacheGroupCache, cacheOptions, cancellationToken);
        _logger.LogInformation($"Added to Cache -> {request.CacheGroupKey}");

        await _cache.SetAsync(
            key: $"{request.CacheGroupKey}SlidingExpiration",
            serializeCachedGroupSlidingExpirationData,
            cacheOptions,
            cancellationToken
        );
        _logger.LogInformation($"Added to Cache -> {request.CacheGroupKey}SlidingExpiration");
    }
    }

