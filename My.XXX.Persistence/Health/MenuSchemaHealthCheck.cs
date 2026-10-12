using My.XXX.Persistence.PersistenceModels;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Persistence.Health;

public sealed class MenuSchemaHealthCheck(DBContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await db.GetTable<MenuWriteLock>().Where(row => row.Id == 1).Select(row => row.Version).SingleAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Menu write lock unavailable. Apply migration 004.", ex);
        }
    }
}
