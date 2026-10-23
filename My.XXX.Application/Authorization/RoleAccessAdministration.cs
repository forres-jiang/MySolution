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

/// <summary>在同一事务中替换菜单选择与权限码。</summary>
public sealed class RoleAccessAdministration(IAccessControlTransaction transaction, UseCaseAuthorization authorization, TimeProvider clock)
{
    private readonly RoleMenuMutations menus = new(clock);
    private readonly PermissionMutations permissions = new();

    public async Task<Result> ReplaceAsync(Guid roleId, List<int> menuIds, List<string> codes,
        CancellationToken cancellationToken = default)
    {
        var access = await authorization.RequireAnyAsync(cancellationToken, PermissionCodes.PermissionsWrite);
        if (access.IsFailed) return Result.Fail(access.Errors);
        var menuAccess = await authorization.RequireAnyAsync(cancellationToken, PermissionCodes.MenuRoleMenus);
        if (menuAccess.IsFailed) return Result.Fail(menuAccess.Errors);
        var validation = RoleMenuChanges.Validate(roleId, menuIds, RoleMenuChange.Replace).ToResult();
        if (validation.IsSuccess) validation = PermissionSelection.Validate(roleId, codes).ToResult();
        if (validation.IsFailed) return validation;
        var result = await transaction.Execute(async session =>
        {
            var menuResult = await menus.SetRoleMenus(session, roleId, menuIds, RoleMenuChange.Replace,
                access.Value.UserId, cancellationToken);
            if (menuResult.IsFailed)
            {
                return menuResult;
            }
            return await permissions.ReplaceAsync(session, roleId, codes, cancellationToken);
        }, cancellationToken);
        if (result.IsSuccess)
        {
            authorization.InvalidatePermissions();
        }
        return result;
    }
}
