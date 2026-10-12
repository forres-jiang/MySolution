using My.XXX.Application.Authentication;
using My.XXX.Application.Authentication.Models;
using My.XXX.Application.Authentication.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace My.XXX.Application.Registration;

internal static class AuthenticationRegistration
{
    internal static void AddAuthenticationUseCases(this IServiceCollection services)
    {
        services.AddOptions<SessionOptions>()
            .Validate(options => options.RefreshExpiryInMinutes > 0, "Configure a positive session lifetime.")
            .ValidateOnStart();
        services.AddSingleton(provider => new SessionLifetime(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SessionOptions>>().Value.RefreshExpiryInMinutes));
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ISessionService, SessionService>();
    }
}
