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

/// <summary>在共享的加锁事务内执行的角色/菜单分配规则。</summary>
internal sealed class RoleMenuMutations(TimeProvider clock)
{
    /// <summary>Compose rules without opening or committing another transaction.</summary>
    public async Task<Result> SetRoleMenus(IRoleMenuWriteSession session, Guid roleId, List<int>? ids,
        RoleMenuChange change, string userId, CancellationToken cancellationToken = default)
    {
        var validation = RoleMenuChanges.Validate(roleId, ids, change).ToResult();
        if (validation.IsFailed) return validation;
        var available = change == RoleMenuChange.Remove ? new List<int>() : await session.LoadMenuIds(cancellationToken);
        var existing = await session.LoadRoleMenus(roleId, cancellationToken);
        var decision = RoleMenuChanges.Build(roleId, ids, change, available, existing).ToResult();
        if (decision.IsFailed) return Result.Fail(decision.Errors);
        return Result.OkIf(await session.ApplyRoleChanges(roleId, decision.Value.Additions, decision.Value.Removals,
            userId, clock.GetUtcNow().UtcDateTime, cancellationToken), RoleMenuErrors.WriteFailed().ToError());
    }

}
