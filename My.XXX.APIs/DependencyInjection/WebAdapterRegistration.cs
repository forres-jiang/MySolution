
using My.XXX.APIs.Common.Authentication;
using My.XXX.Application.Abstractions.Ports;
using My.XXX.Application.Authentication.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace My.XXX.APIs.DependencyInjection;

public static class WebAdapterRegistration
{
    public static IServiceCollection AddWebAdapters(this IServiceCollection services)
    {
        services.AddScoped<HttpCurrentRequest>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<HttpCurrentRequest>());
        services.AddScoped<IAuthenticationSession>(sp => sp.GetRequiredService<HttpCurrentRequest>());
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        return services;
    }
}
