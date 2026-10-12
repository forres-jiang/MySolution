using My.XXX.Application.Authorization.Ports;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Infrastructure.Caching;

// 仅在未配置 Redis 且权限来自数据库时使用。
public sealed class NullPermissionCache : IPermissionCache
{
    public Task<List<string>?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<List<string>?>(null);

    public Task SetAsync(string key, List<string> codes, TimeSpan expiration, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
