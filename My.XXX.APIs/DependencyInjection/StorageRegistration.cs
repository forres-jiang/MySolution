using My.XXX.APIs.Configurations;
using My.XXX.Infrastructure;
using My.XXX.Infrastructure.Caching;
using My.XXX.Infrastructure.Health;
using My.XXX.Infrastructure.Security;
using My.XXX.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace My.XXX.APIs.DependencyInjection;

public static class StorageRegistration
{
    public static string ResolveConnectionString(string? value, string name, string? encodedKey = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Configure the {name} connection string through a secret provider.");
        return value.StartsWith("enc:v1:", StringComparison.Ordinal) ? AESHelper.Decrypt(value, encodedKey) : value;
    }

    public static bool AddDBs(this IServiceCollection services, IConfiguration configuration)
    {
        var defaultProvider = DatabaseConfiguration.ParseProvider(configuration["DatabaseProviders:Default"]);
        var connStrings = configuration.GetSection("ConnectionStrings").Get<ConnectionStrings>();

        //DB
        var defaultConnection = ResolveConnectionString(connStrings?.Default, "Default", configuration["APP_ENCRYPTION_KEY"]);
        services.AddPersistenceDatabases(defaultConnection, defaultProvider);
        services.AddPersistenceHealthChecks(defaultConnection, defaultProvider);

        //Redis
        var redisConfig = configuration.GetSection("RedisConfig").Get<RedisConfig>();
        var redisConnection = string.IsNullOrWhiteSpace(redisConfig?.ConnectionString) ? null
            : ResolveConnectionString(redisConfig.ConnectionString, "Redis", configuration["APP_ENCRYPTION_KEY"]);
        var useCachedPermissionQuery = services.AddPermissionCaching(redisConnection,
            configuration.GetValue<PermissionDataCache>("AppConfig:PermissionDataCache") == PermissionDataCache.Redis);
        // Redis 仅服务于权限缓存，启用时纳入可降级依赖的诊断检查。
        if (useCachedPermissionQuery)
        {
            services.AddRedisHealthChecks();
        }
        return useCachedPermissionQuery;
    }

}
