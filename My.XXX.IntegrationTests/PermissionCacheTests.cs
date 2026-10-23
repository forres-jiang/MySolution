using My.XXX.Application;
using My.XXX.Application.Authorization;
using My.XXX.Application.Authorization.Ports;
using My.XXX.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.IntegrationTests;

[TestClass]
public sealed class PermissionCacheTests
{
    [TestMethod]
    public async Task CacheToggleKeepsTheApplicationServiceAndItsResults()
    {
        foreach (var enabled in new[] { false, true })
        {
            var store = new Store();
            var cache = new Cache();
            var services = new ServiceCollection().AddApplicationServices();
            // CachedPermissionStore 依赖 ILogger<T>；最小化容器需显式提供日志实现，与下方 Decorate() 传 NullLogger 的做法等价。
            services.AddLogging();
            services.AddSingleton<IPermissionStore>(store);
            services.AddSingleton<IPermissionCache>(cache);
            services.AddSingleton<IOptions<PermissionCacheOptions>>(Options.Create(new PermissionCacheOptions()));
            if (enabled) services.Decorate<IPermissionStore, CachedPermissionStore>();
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var query = scope.ServiceProvider.GetRequiredService<IPermissionQuery>();
            Assert.IsInstanceOfType<PermissionQuery>(query);
            var roles = new List<Guid> { Guid.NewGuid() };
            CollectionAssert.AreEqual(new[] { "menu.get" }, await query.GetPermissionCodesAsync(roles, "alice"));
            CollectionAssert.AreEqual(new[] { "menu.get" }, await query.GetPermissionCodesAsync(roles, "bob"));
            Assert.AreEqual(enabled ? 1 : 2, store.Reads);
        }
    }

    [TestMethod]
    public async Task RevisionChangeDuringReadRetriesWithoutPublishingOldData()
    {
        var store = new Store { Revisions = new Queue<long>(new long[] { 1, 2, 2, 2 }) };
        store.OnRead = count => count == 1 ? new() { "old" } : new() { "new" };
        var cache = new Cache();
        var result = await Decorate(store, cache).GetPermissionCodesAsync(new() { Guid.NewGuid() });
        CollectionAssert.AreEqual(new[] { "new" }, result);
        Assert.AreEqual(2, store.Reads);
        Assert.AreEqual(1, cache.Writes);
    }

    [TestMethod]
    public async Task RepeatedRevisionChangesFallBackWithoutCaching()
    {
        var store = new Store { Revisions = new Queue<long>(new long[] { 1, 2, 2, 3, 3, 4 }) };
        var cache = new Cache();
        await Decorate(store, cache).GetPermissionCodesAsync(new() { Guid.NewGuid() });
        Assert.AreEqual(4, store.Reads);
        Assert.AreEqual(0, cache.Writes);
    }

    [TestMethod]
    public async Task CachedValuesCannotBeModifiedByCallersAndRoleSetsAreCanonical()
    {
        var store = new Store();
        var cache = new Cache();
        var query = Decorate(store, cache);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var first = await query.GetPermissionCodesAsync(new() { a, b });
        first.Clear();
        var second = await query.GetPermissionCodesAsync(new() { b, a, a });
        CollectionAssert.AreEqual(new[] { "menu.get" }, second);
        Assert.AreEqual(1, store.Reads);
        store.Revision = 2;
        store.OnRead = _ => new() { "menu.edit" };
        CollectionAssert.AreEqual(new[] { "menu.edit" }, await query.GetPermissionCodesAsync(new() { a, b }));
        Assert.AreEqual(2, store.Reads);
    }

    private static CachedPermissionStore Decorate(Store store, Cache cache) =>
        new(store, cache, Options.Create(new PermissionCacheOptions()), NullLogger<CachedPermissionStore>.Instance);

    [TestMethod]
    public async Task RedisFailuresFallBackToTheDatabaseAndDoNotLosePermissions()
    {
        var store = new Store();
        var cache = new Cache
        {
            ReadFailure = new InvalidOperationException("模拟缓存读取故障。"),
            WriteFailure = new InvalidOperationException("模拟缓存写入故障。")
        };
        var result = await Decorate(store, cache).GetPermissionCodesAsync(new() { Guid.NewGuid() });
        CollectionAssert.AreEqual(new[] { "menu.get" }, result);
        Assert.AreEqual(1, store.Reads);
    }

    [TestMethod]
    public async Task RequestCancellationDuringCacheReadsOrWritesIsNotSwallowed()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        foreach (var reading in new[] { true, false })
        {
            var exception = new OperationCanceledException(cancellation.Token);
            var cache = new Cache { ReadFailure = reading ? exception : null, WriteFailure = reading ? null : exception };
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                Decorate(new Store(), cache).GetPermissionCodesAsync(new() { Guid.NewGuid() }, cancellation.Token));
        }
    }

    private sealed class Store : IPermissionStore
    {
        public int Reads;
        public long Revision = 1;
        public Queue<long> Revisions = new();
        public Func<int, List<string>> OnRead = _ => new() { "menu.get" };
        public Task<long> GetPermissionRevisionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Revisions.Count > 0 ? Revisions.Dequeue() : Revision);
        public Task<List<string>> GetPermissionCodesAsync(List<Guid> roleIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(OnRead(++Reads));
    }

    private sealed class Cache : IPermissionCache
    {
        private readonly Dictionary<string, List<string>> values = new();
        public int Writes;
        public Exception? ReadFailure { get; init; }
        public Exception? WriteFailure { get; init; }
        public Task<List<string>?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            ReadFailure is { } failure ? Task.FromException<List<string>?>(failure)
                : Task.FromResult(values.TryGetValue(key, out var value) ? value : null);
        public Task SetAsync(string key, List<string> value, TimeSpan expiration, CancellationToken cancellationToken = default)
        {
            if (WriteFailure is { } failure)
            {
                return Task.FromException(failure);
            }
            values[key] = value;
            Writes++;
            return Task.CompletedTask;
        }
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
