using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Infrastructure.Health;

public sealed class RedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await connection.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded("Redis 不可用，权限查询已降级为数据库直读。", ex);
        }
    }
}
