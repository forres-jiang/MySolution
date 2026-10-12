using My.XXX.Application.Abstractions.Results;
using My.XXX.Application.AccessControl.Ports;
using My.XXX.Domain.Authorization.Policies;
using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Application.Authorization;

/// <summary>Permission rules composed inside an existing access-control transaction.</summary>
internal sealed class PermissionMutations
{
    public async Task<Result> ReplaceAsync(IPermissionWriteSession session, Guid roleId,
        List<string> codes, CancellationToken cancellationToken = default)
    {
        var validation = PermissionSelection.Validate(roleId, codes).ToResult();
        if (validation.IsFailed) return validation;
        await session.ReplaceRolePermissions(roleId, codes.Distinct().ToList(), cancellationToken);
        return Result.Ok();
    }

}
