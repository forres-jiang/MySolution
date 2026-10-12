using My.XXX.Application.Authorization.Ports;
using My.XXX.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;

namespace My.XXX.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddExternalAdapters(this IServiceCollection services)
    {
        services.AddMemoryCache();
        return services;
    }
    // 只注册基础设施自己的适配器；存储装饰器的组装属于组合根。返回缓存是否可用。
    public static bool AddPermissionCaching(this IServiceCollection services, string? connectionString, bool enabled)
    {
        services.AddOptions<PermissionCacheOptions>();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (enabled) throw new InvalidOperationException("Redis permission caching requires RedisConfig:ConnectionString.");
            services.AddSingleton<IPermissionCache, NullPermissionCache>();
            return false;
        }
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.BacklogPolicy = BacklogPolicy.FailFast;
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        services.AddSingleton<IPermissionCache, PermissionCache>();
        return enabled;
    }
}
