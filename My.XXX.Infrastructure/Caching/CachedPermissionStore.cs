using My.XXX.Application.Authorization.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
namespace My.XXX.Infrastructure.Caching;

/// <summary>Caches role projections below the application service so every request still runs application rules.</summary>
public sealed class CachedPermissionStore(IPermissionStore menus, IPermissionCache cache,
    IOptions<PermissionCacheOptions> options, ILogger<CachedPermissionStore> logger) : IPermissionStore
{
    private static readonly ActivitySource Activities = new("My.XXX.Authorization");
    private static readonly Meter Meter = new("My.XXX.Authorization");
    private static readonly Counter<long> Hits = Meter.CreateCounter<long>("permissions.cache.hits");
    private static readonly Counter<long> Misses = Meter.CreateCounter<long>("permissions.cache.misses");
    private static readonly Counter<long> Retries = Meter.CreateCounter<long>("permissions.revision.retries");
    private static readonly Counter<long> Fallbacks = Meter.CreateCounter<long>("permissions.database.fallbacks");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("permissions.query.duration", "ms");

    public Task<long> GetPermissionRevisionAsync(CancellationToken cancellationToken = default) =>
        menus.GetPermissionRevisionAsync(cancellationToken);

    public async Task<List<string>> GetPermissionCodesAsync(List<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        using var activity = Activities.StartActivity("permissions.query");
        var start = Stopwatch.GetTimestamp();
        try { return await QueryAsync(roleIds, cancellationToken); }
        finally { Duration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
    }

    private async Task<List<string>> QueryAsync(List<Guid> roleIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roleIds);
        var config = options.Value;
        if (config.ExpiryInMinutes <= 0 || string.IsNullOrWhiteSpace(config.KeyPrefix))
            throw new InvalidOperationException("Permission cache requires a positive expiry and a key prefix.");
        // 在缓存/数据库读取之后重新检查。绝不能以不属于某次修订（revision）的版本发布数据。
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var revision = await menus.GetPermissionRevisionAsync(cancellationToken);
            var key = PermissionCacheKey.CreateRoleSet(config.KeyPrefix, roleIds, revision);
            var cached = await TryGetCachedAsync(key, cancellationToken);
            if (cached == null) Misses.Add(1); else Hits.Add(1);
            var paths = cached ?? await menus.GetPermissionCodesAsync(roleIds, cancellationToken);
            if (revision != await menus.GetPermissionRevisionAsync(cancellationToken)) { Retries.Add(1); continue; }
            if (cached == null) await TrySetCachedAsync(key, new List<string>(paths), TimeSpan.FromMinutes(config.ExpiryInMinutes), cancellationToken);
            return new List<string>(paths);
        }
        // 持续编辑的情况下：直接读取当前权限，而不是返回过期的缓存授权。
        Fallbacks.Add(1);
        return await menus.GetPermissionCodesAsync(roleIds, cancellationToken);
    }

    // Redis 故障时降级到数据库直读，避免缓存层异常导致所有权限校验失败。
    private async Task<List<string>?> TryGetCachedAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await cache.GetAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            Fallbacks.Add(1);
            logger.LogWarning(ex, "权限缓存读取失败，降级到数据库直读");
            return null;
        }
    }

    private async Task TrySetCachedAsync(string key, List<string> codes, TimeSpan expiration, CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetAsync(key, codes, expiration, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "权限缓存写入失败，跳过本次缓存");
        }
    }
}
