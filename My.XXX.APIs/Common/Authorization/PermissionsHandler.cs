using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
namespace My.XXX.APIs.Common.Authorization;

public sealed class PermissionsHandler(My.XXX.Application.Authorization.UseCaseAuthorization authorization) : AuthorizationHandler<PermissionsRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionsRequirement requirement)
    {
        if (context.Resource is not HttpContext http || context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }
        var code = http.GetEndpoint()?.Metadata.GetMetadata<RequiresPermissionAttribute>()?.Code;
        if (string.IsNullOrWhiteSpace(code) || requirement.Name != PolicyType.Default)
        {
            return;
        }
        if ((await authorization.RequireAnyAsync(http.RequestAborted, code)).IsSuccess)
        {
            context.Succeed(requirement);
        }
    }
}
