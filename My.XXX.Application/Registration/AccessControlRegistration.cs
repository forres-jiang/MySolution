using My.XXX.Application.Authorization;
using My.XXX.Application.Authorization.Ports;
using My.XXX.Application.Menus;
using My.XXX.Application.Menus.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace My.XXX.Application.Registration;

internal static class AccessControlRegistration
{
    internal static void AddAccessControlUseCases(this IServiceCollection services)
    {
        services.AddScoped<UseCaseAuthorization>();
        services.AddSingleton<ApplicationMapper>();
        services.AddScoped<MenuCommandService>();
        services.AddScoped<RoleAccessAdministration>();
        services.AddScoped<MenuQueryService>();
        services.AddScoped<RoleMenuAssignmentService>();
        services.AddScoped<IPermissionAdministration, PermissionAdministration>();
        services.TryAddScoped<IPermissionQuery, PermissionQuery>();
    }
}
