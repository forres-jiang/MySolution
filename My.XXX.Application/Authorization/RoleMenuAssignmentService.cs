using My.XXX.Application.Abstractions.Results;
using My.XXX.Application.AccessControl.Ports;
using My.XXX.Domain.Authorization.Models;
using My.XXX.Domain.Authorization.Policies;
using FluentResults;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace My.XXX.Application.Authorization;

public sealed class RoleMenuAssignmentService(IRoleMenuTransaction transaction, UseCaseAuthorization authorization, TimeProvider clock)
{
    private readonly RoleMenuMutations mutations = new(clock);

    public async Task<Result> SetRoleMenus(Guid roleId, List<int>? menuIds, RoleMenuChange change, CancellationToken cancellationToken = default)
    {
        var required = change switch
        {
            RoleMenuChange.Add => new[] { PermissionCodes.MenuRoleMenu, PermissionCodes.MenuRoleMenuChecked },
            RoleMenuChange.Remove => new[] { PermissionCodes.MenuRemoveRoleMenu, PermissionCodes.MenuRoleMenu, PermissionCodes.MenuRoleMenuChecked },
            _ => new[] { PermissionCodes.MenuRoleMenus }
        };
        var access = await authorization.RequireAnyAsync(cancellationToken, required);
        if (access.IsFailed) return Result.Fail(access.Errors);
        var validation = RoleMenuChanges.Validate(roleId, menuIds, change).ToResult();
        if (validation.IsFailed) return validation;
        return (await transaction.ExecuteRoleMenus(session => mutations.SetRoleMenus(session, roleId, menuIds, change, access.Value.UserId, cancellationToken), cancellationToken));
    }

    public Task<Result> SetRoleMenusChecked(Guid roleId, List<int>? menuIds, bool? isChecked, CancellationToken cancellationToken = default) =>
        isChecked.HasValue
            ? SetRoleMenus(roleId, menuIds, isChecked.Value ? RoleMenuChange.Add : RoleMenuChange.Remove, cancellationToken)
            : Task.FromResult(Result.Fail(RoleMenuErrors.InvalidSelection().ToError()));

    public Task<Result> ReplaceRoleMenus(Guid roleId, List<int>? menuIds, CancellationToken cancellationToken = default) =>
        SetRoleMenus(roleId, menuIds, RoleMenuChange.Replace, cancellationToken);

    public Task<Result> RemoveRoleMenus(Guid roleId, List<int>? menuIds, CancellationToken cancellationToken = default) =>
        SetRoleMenus(roleId, menuIds, RoleMenuChange.Remove, cancellationToken);
}
