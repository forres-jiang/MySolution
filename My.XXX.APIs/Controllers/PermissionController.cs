using My.XXX.APIs.Common.Authorization;
using My.XXX.APIs.Common.Responses;
using My.XXX.APIs.Contracts.Authorization;
using My.XXX.APIs.Contracts.Shared;
using My.XXX.Application.Authorization.Ports;
using My.XXX.Domain.Authorization.Policies;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace My.XXX.APIs.Controllers;

[ApiController]
[Route("api/v2/permissions")]
public sealed class PermissionController(IPermissionAdministration permissions) : ControllerBase
{
    [HttpGet("catalog")]
    [RequiresPermission(PermissionCodes.PermissionsRead)]
    public APIResponse<IReadOnlyList<string>> Catalog() => new(1, "Success", PermissionCodes.All, traceId: HttpContext.TraceIdentifier);

    [HttpGet("roles/{roleId:guid}")]
    [RequiresPermission(PermissionCodes.PermissionsRead)]
    public async Task<ActionResult<APIResponse<List<string>>>> Get(Guid roleId) =>
        (await permissions.GetAsync(roleId, HttpContext.RequestAborted)).ToHttpResult(HttpContext.TraceIdentifier);

    [HttpPut("roles/{roleId:guid}")]
    [RequiresPermission(PermissionCodes.PermissionsWrite)]
    public async Task<ActionResult<APIResponse<bool>>> Replace(Guid roleId, [FromBody] ReplacePermissionsRequest model)
    {
        var result = await permissions.ReplaceAsync(roleId, model.Codes, HttpContext.RequestAborted);
        return result.ToHttpResult(HttpContext.TraceIdentifier);
    }
}
