using My.XXX.Application.Authorization.Ports;
using My.XXX.Infrastructure;
using My.XXX.Infrastructure.Caching;
using My.XXX.Infrastructure.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace My.XXX.IntegrationTests;

[TestClass]
public sealed class OptionalDependencyTests
{
    [TestMethod]
    public async Task RedisFailureDegradesDiagnosticsWithoutFailingReadiness()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(DispatchProxy.Create<IConnectionMultiplexer, UnavailableRedisConnection>());
        services.AddHealthChecks().AddCheck("database", () => HealthCheckResult.Healthy(), tags: new[] { "ready" });
        services.AddRedisHealthChecks();
        using var provider = services.BuildServiceProvider();
        var health = provider.GetRequiredService<HealthCheckService>();

        var readiness = await health.CheckHealthAsync(registration => registration.Tags.Contains("ready"));
        Assert.AreEqual(HealthStatus.Healthy, readiness.Status);
        Assert.IsFalse(readiness.Entries.ContainsKey("redis"));

        var diagnostics = await health.CheckHealthAsync();
        Assert.AreEqual(HealthStatus.Degraded, diagnostics.Status);
        Assert.AreEqual(HealthStatus.Degraded, diagnostics.Entries["redis"].Status);
    }

    [TestMethod]
    public void DisabledCachingDoesNotCreateRedisConnectionsEvenWhenConfigured()
    {
        var services = new ServiceCollection();
        Assert.IsFalse(services.AddPermissionCaching("localhost:6379", enabled: false));
        using var provider = services.BuildServiceProvider();
        Assert.IsNull(provider.GetService<IConnectionMultiplexer>());
        Assert.IsInstanceOfType<NullPermissionCache>(provider.GetRequiredService<IPermissionCache>());
    }

    public class UnavailableRedisConnection : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new TimeoutException("模拟 Redis 连接超时。");
    }
}
