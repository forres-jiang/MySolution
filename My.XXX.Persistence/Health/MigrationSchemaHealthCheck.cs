using My.XXX.Persistence.Migrations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Persistence.Health;

public sealed class MigrationSchemaHealthCheck(string connectionString, DatabaseProvider provider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await MigrationRunner.VerifyAsync(connectionString, provider, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Required schema migrations are missing or changed. Run the deployment migration command.", ex);
        }
    }
}
