using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;

namespace My.XXX.Infrastructure.Health;

public static class RedisHealthRegistration
{
    public static IServiceCollection AddRedisHealthChecks(this IServiceCollection services)
    {
        // 缓存故障可以降级为数据库直读，仅影响诊断状态，不摘除仍可服务的实例。
        services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis", failureStatus: HealthStatus.Degraded,
            tags: ["optional"], timeout: TimeSpan.FromSeconds(5));
        return services;
    }
}
