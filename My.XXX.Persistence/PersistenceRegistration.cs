using My.XXX.Application.AccessControl.Ports;
using My.XXX.Application.Authentication.Ports;
using My.XXX.Application.Authorization.Ports;
using My.XXX.Application.Menus.Ports;
using My.XXX.Persistence.Repositories;
using LinqToDB.Extensions.DependencyInjection;
using LinqToDB.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace My.XXX.Persistence;

public static class PersistenceRegistration
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.TryAddScoped<IAuthenticationStore, AuthenticationStore>();
        services.AddScoped<IRolePermissionStore, RolePermissionStore>();
        services.AddScoped<IMenuReadRepository, MenuReadRepository>();
        services.AddScoped<IPermissionStore, PermissionStore>();
        services.AddScoped<IAccessControlTransaction, AccessControlTransaction>();
        return services;
    }
    public static IServiceCollection AddPersistenceDatabases(this IServiceCollection services, string businessConnection,
        DatabaseProvider businessProvider)
    {
        services.AddLinqToDBContext<DBContext>((provider, options) =>
            DatabaseConfiguration.Configure(options, businessConnection, businessProvider).UseDefaultLogging(provider));
        return services;
    }
}
