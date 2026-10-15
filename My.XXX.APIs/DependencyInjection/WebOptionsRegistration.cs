using My.XXX.APIs.Configurations;
using My.XXX.Application.Authentication.Models;
using My.XXX.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace My.XXX.APIs.DependencyInjection;

public static class WebOptionsRegistration
{
    public static void AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new AesEncryptionKey(configuration["APP_ENCRYPTION_KEY"] ?? ""));

        //AppConfig
        services.Configure<AppConfig>(configuration.GetSection("AppConfig"));
        //ConnStrings
        services.Configure<ConnectionStrings>(configuration.GetSection("ConnectionStrings"));
        //JWT
        services.Configure<JwtConfig>(configuration.GetSection("JwtConfig"));
        services.Configure<SessionOptions>(configuration.GetSection("JwtConfig"));
        services.AddOptions<My.XXX.Infrastructure.Caching.PermissionCacheOptions>()
            .Bind(configuration.GetSection("PermissionCache"))
            .Validate(o => o.ExpiryInMinutes > 0 && !string.IsNullOrWhiteSpace(o.KeyPrefix),
                "PermissionCache needs a positive expiry and a key prefix.")
            .ValidateOnStart();
    }
}
