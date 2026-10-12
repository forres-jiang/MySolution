using My.XXX.Application.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace My.XXX.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.TryAddSingleton(System.TimeProvider.System);
        services.AddAuthenticationUseCases();
        services.AddAccessControlUseCases();
        return services;
    }
}
