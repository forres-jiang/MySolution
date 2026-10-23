using My.XXX.Application.Abstractions.Results;
using My.XXX.Application.AccessControl.Ports;
using My.XXX.Application.Authorization.Ports;
using FluentResults;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace My.XXX.Application.Authorization;

public sealed class PermissionAdministration(IRolePermissionStore store, IPermissionTransaction transaction, UseCaseAuthorization authorization) : IPermissionAdministration
{
    private readonly PermissionMutations mutations = new();

    public async Task<Result<List<string>>> GetAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var access = await authorization.RequireAnyAsync(cancellationToken, My.XXX.Domain.Authorization.Policies.PermissionCodes.PermissionsRead);
        if (access.IsFailed) return Result.Fail<List<string>>(access.Errors);
        return Result.Ok(await store.GetAsync(roleId, cancellationToken));
    }
    public async Task<Result> ReplaceAsync(Guid roleId, List<string> codes, CancellationToken cancellationToken = default)
    {
        var access = await authorization.RequireAnyAsync(cancellationToken, My.XXX.Domain.Authorization.Policies.PermissionCodes.PermissionsWrite);
        if (access.IsFailed)
        {
            return Result.Fail(access.Errors);
        }
        var validation = My.XXX.Domain.Authorization.Policies.PermissionSelection.Validate(roleId, codes).ToResult();
        if (validation.IsFailed)
        {
            return validation;
        }
        var result = await transaction.ExecutePermissions(session => mutations.ReplaceAsync(session, roleId, codes, cancellationToken), cancellationToken);
        if (result.IsSuccess)
        {
            authorization.InvalidatePermissions();
        }
        return result;
    }
}
