using My.XXX.APIs.Common.Authorization;
using My.XXX.APIs.Configurations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace My.XXX.APIs.DependencyInjection;

public static class WebSecurityRegistration
{
    public static void AddWebSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        //权限
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy("Permissions", polic =>
            {
                polic.RequireAuthenticatedUser();
                polic.AddRequirements(new PermissionsRequirement(PolicyType.Default));
            });

        services.AddScoped<IAuthorizationHandler, PermissionsHandler>();

        services.AddDataProtection()
            .SetApplicationName(configuration["DataProtection:ApplicationName"] ?? "XXX")
            .SetDefaultKeyLifetime(TimeSpan.FromDays(configuration.GetValue("DataProtection:KeyLifetimeDays", 90)))
            .PersistKeysToFileSystem(new DirectoryInfo(configuration["DataProtection:KeyPath"] ?? Path.Combine(AppContext.BaseDirectory, "keys")))
            .ProtectKeysWithAES();

        //身份验证
        var jwtConfig = configuration.GetSection("JwtConfig").Get<JwtConfig>()!;
        My.XXX.APIs.Common.Authentication.TokenAuthentication.Validate(jwtConfig);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => My.XXX.APIs.Common.Authentication.TokenAuthentication.Configure(options, jwtConfig, "access"))
            .AddJwtBearer("Refresh", options => My.XXX.APIs.Common.Authentication.TokenAuthentication.Configure(options, jwtConfig, "refresh"));

    }
}
