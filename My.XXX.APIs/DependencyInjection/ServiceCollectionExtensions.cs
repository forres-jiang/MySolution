using My.XXX.APIs.Common.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace My.XXX.APIs.DependencyInjection;

/// <summary>Composes HTTP registrations; each concern owns its configuration.</summary>
public static class ServiceCollectionExtensions
{
    public static void AddDefaultService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContracts(configuration);
        services.AddWebSecurity(configuration);
        services.AddHttpPolicies(configuration);
        services.AddApiDocumentation(configuration);
        services.AddHealthChecks();
        services.AddHttpContextAccessor();
        services.AddScoped<ExceptionHandlingMiddleware>();
    }
}
