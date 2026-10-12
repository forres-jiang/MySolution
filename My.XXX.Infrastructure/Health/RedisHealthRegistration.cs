using Microsoft.Extensions.DependencyInjection;
using System;

namespace My.XXX.Infrastructure.Health;

public static class RedisHealthRegistration
{
    public static IServiceCollection AddRedisHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
        return services;
    }
}