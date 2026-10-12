using My.XXX.Application.AccessControl.Ports;
using My.XXX.Application.Authorization;
using My.XXX.Application.Menus.Models;
using My.XXX.Domain.Authorization.Policies;
using My.XXX.Domain.Menus.Models;
using FluentResults;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Application.Menus;

/// <summary>Owns the request context and transaction boundary for menu commands.</summary>
public sealed class MenuCommandService(IAccessControlTransaction transaction, UseCaseAuthorization authorization, TimeProvider clock)
{
    private readonly MenuMutations mutations = new(clock);

    public Task<Result> Add(MenuCreation menu, CancellationToken cancellationToken = default) =>
        Execute(PermissionCodes.MenuAdd, MenuMutations.ValidateAdd(menu), (session, userId) => mutations.Add(session, menu, userId, cancellationToken), cancellationToken);
    public Task<Result> Update(MenuModification menu, CancellationToken cancellationToken = default) =>
        Execute(PermissionCodes.MenuEdit, MenuMutations.ValidateUpdate(menu), (session, userId) => mutations.Update(session, menu, userId, cancellationToken), cancellationToken);
    public Task<Result> Remove(List<int> ids, CancellationToken cancellationToken = default) =>
        Execute(PermissionCodes.MenuRemove, MenuMutations.ValidateRemove(ids), (session, userId) => mutations.Remove(session, ids, userId, cancellationToken), cancellationToken);
    public Task<Result> UpdateSort(MenuReorderCommand command, CancellationToken cancellationToken = default) =>
        Execute(PermissionCodes.MenuUpdateSort, Result.Ok(), (session, userId) => mutations.Move(session, command, userId, cancellationToken), cancellationToken);
    private async Task<Result> Execute(string permission, Result validation, Func<IMenuWriteSession, string, Task<Result>> operation,
        CancellationToken cancellationToken)
    {
        var access = await authorization.RequireAnyAsync(cancellationToken, permission);
        if (access.IsFailed) return Result.Fail(access.Errors);
        if (validation.IsFailed) return validation;
        return await transaction.ExecuteMenus(session => operation(session, access.Value.UserId), cancellationToken);
    }
}
