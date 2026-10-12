using My.XXX.Domain.Menus.Models;
using FluentResults;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Application.AccessControl.Ports;

public interface IAccessControlTransaction
{
    /// <summary>组合菜单与权限写入；失败结果和异常会回滚所有写入。</summary>
    Task<Result> Execute(Func<IAccessControlWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default);
    Task<Result> ExecuteMenus(Func<IMenuWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default);
    Task<Result> ExecuteRoleMenus(Func<IRoleMenuWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default);
    Task<Result> ExecutePermissions(Func<IPermissionWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default);
}

/// <summary>仅在 Execute 内部可用。包含持久化原语，而非业务命令。</summary>
public interface IAccessControlWriteSession : IMenuWriteSession, IRoleMenuWriteSession, IPermissionWriteSession { }

public interface IMenuWriteSession
{
    Task<List<MenuState>> LoadMenus(CancellationToken cancellationToken = default);
    Task<List<MenuStructure>> LoadMenuStructure(CancellationToken cancellationToken = default);
    Task<MenuState?> LoadMenu(int id, CancellationToken cancellationToken = default);
    Task<int> Insert(MenuState menu, CancellationToken cancellationToken = default);
    Task<int> Update(MenuState menu, CancellationToken cancellationToken = default);
    Task<int> Remove(List<int> ids, string userId, DateTime timestamp, CancellationToken cancellationToken = default);
}

public interface IRoleMenuWriteSession
{
    Task<List<int>> LoadMenuIds(CancellationToken cancellationToken = default);
    Task<List<int>> LoadRoleMenus(Guid roleId, CancellationToken cancellationToken = default);
    Task<bool> ApplyRoleChanges(Guid roleId, List<int> additions, List<int> removals, string userId, DateTime timestamp, CancellationToken cancellationToken = default);
}

public interface IPermissionWriteSession
{
    Task ReplaceRolePermissions(Guid roleId, List<string> codes, CancellationToken cancellationToken = default);
}
