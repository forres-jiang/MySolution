using My.XXX.Application.Authorization.Ports;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Infrastructure.Caching;

public sealed class PermissionCache(IConnectionMultiplexer connection) : IPermissionCache
{
    public async Task<List<string>?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        var value = await connection.GetDatabase().StringGetAsync(key).WaitAsync(cancellationToken);
        // 值使用 JSON 数组，键的语义由调用方决定。
        return value.IsNull ? null : JsonConvert.DeserializeObject<List<string>>(value.ToString());
    }

    public async Task SetAsync(string key, List<string> codes, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(codes);
        if (expiration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(expiration));
        cancellationToken.ThrowIfCancellationRequested();
        // SET 原子地包含 TTL；绝不能无限期缓存权限。
        await connection.GetDatabase().StringSetAsync(key, JsonConvert.SerializeObject(codes), expiration)
            .WaitAsync(cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        await connection.GetDatabase().KeyDeleteAsync(key).WaitAsync(cancellationToken);
    }
}
