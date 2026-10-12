using My.XXX.Application.Authorization.Ports;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;
namespace My.XXX.Persistence.Health;

public sealed class PermissionSchemaHealthCheck(IPermissionStore repository) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await repository.GetPermissionRevisionAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Permission schema unavailable. Apply the PermissionRevision database upgrade.", ex);
        }
    }
}
