using My.XXX.Application.Authentication.Ports;
using My.XXX.Application.Authorization.Ports;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;
namespace My.XXX.Persistence.Health;

public sealed class AuthenticationSchemaHealthCheck(IAuthenticationStore sessions, IRolePermissionStore permissions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await sessions.GetActiveSessionAsync("health-schema-probe", DateTime.UtcNow, cancellationToken);
            await permissions.GetAsync(Guid.Empty, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Apply the authentication and stable permission schema upgrades.", ex);
        }
    }
}
