using My.XXX.Application.AccessControl.Ports;
using My.XXX.Domain.Menus.Models;
using My.XXX.Persistence.Mapping;
using My.XXX.Persistence.PersistenceModels;
using FluentResults;
using LinqToDB;
using LinqToDB.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Persistence.Repositories;

public sealed class AccessControlTransaction(DBContext db) : IAccessControlTransaction
{
    private readonly PersistenceMapper mapper = new();
    private bool writing;

    public Task<Result> Execute(Func<IAccessControlWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return ExecuteCore(session => operation(session), menus: true, permissions: true, cancellationToken);
    }

    public Task<Result> ExecuteMenus(Func<IMenuWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return ExecuteCore(session => operation(session), menus: true, permissions: false, cancellationToken);
    }

    public Task<Result> ExecuteRoleMenus(Func<IRoleMenuWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        // Assignments share the menu lock so a concurrent deletion cannot leave a dangling relation.
        return ExecuteCore(session => operation(session), menus: true, permissions: false, cancellationToken);
    }

    public Task<Result> ExecutePermissions(Func<IPermissionWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return ExecuteCore(session => operation(session), menus: false, permissions: true, cancellationToken);
    }

    private async Task<Result> ExecuteCore(Func<WriteSession, Task<Result>> operation, bool menus, bool permissions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (writing) throw new InvalidOperationException("Nested menu transactions are not supported; compose operations within the existing session.");
        writing = true;
        var session = new WriteSession(db, mapper, menus, permissions);
        try
        {
            return await AtomicWrite.ExecuteAsync(db, async () =>
            {
                // Fixed acquisition order for composed commands: menu tree first, permission revision second.
                if (menus && await db.GetTable<MenuWriteLock>().Where(r => r.Id == 1)
                    .Set(r => r.Version, r => r.Version + 1).UpdateAsync(cancellationToken) != 1)
                    throw new InvalidOperationException("The MenuWriteLock row is missing; the database schema is not ready for menu writes.");
                if (permissions && await db.GetTable<PermissionRevision>().Where(r => r.Id == 1)
                    .Set(r => r.Version, r => r.Version + 1).UpdateAsync(cancellationToken) != 1)
                    throw new InvalidOperationException("The PermissionRevision row is missing; the database schema is not ready for permission writes.");
                return await operation(session);
            }, result => result.IsSuccess, cancellationToken);
        }
        finally { session.Close(); writing = false; }
    }

    private sealed class WriteSession(DBContext db, PersistenceMapper mapper, bool menus, bool permissions) : IAccessControlWriteSession
    {
        private bool active = true;
        public void Close() => active = false;
        private void Check() { if (!active) throw new InvalidOperationException("The menu write session has ended."); }
        private void CheckMenus()
        {
            Check();
            if (!menus) throw new InvalidOperationException("This transaction has no menu write capability.");
        }
        private void CheckPermissions()
        {
            Check();
            if (!permissions) throw new InvalidOperationException("This transaction has no permission write capability.");
        }
        public async Task ReplaceRolePermissions(Guid roleId, List<string> codes, CancellationToken cancellationToken = default)
        {
            CheckPermissions();
            await db.GetTable<RolePermission>().Where(p => p.RoleId == roleId).DeleteAsync(cancellationToken);
            foreach (var code in codes)
                await db.InsertAsync(new RolePermission { RoleId = roleId, Code = code }, token: cancellationToken);
        }
        public async Task<List<MenuState>> LoadMenus(CancellationToken cancellationToken = default) { CheckMenus(); return mapper.ToMenuStates(await db.Menus.Where(m => !m.IsDeleted).ToListAsync(cancellationToken)); }
        public Task<List<MenuStructure>> LoadMenuStructure(CancellationToken cancellationToken = default)
        {
            CheckMenus();
            return db.Menus.Where(m => !m.IsDeleted)
                .Select(m => new MenuStructure(m.Id, m.ParentId, m.IsAction)).ToListAsync(cancellationToken);
        }
        public async Task<MenuState?> LoadMenu(int id, CancellationToken cancellationToken = default)
        {
            CheckMenus();
            return mapper.ToMenuState(await db.Menus.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, cancellationToken));
        }
        public Task<List<int>> LoadMenuIds(CancellationToken cancellationToken = default) { CheckMenus(); return db.Menus.Where(m => !m.IsDeleted).Select(m => m.Id).ToListAsync(cancellationToken); }
        public async Task<List<int>> LoadRoleMenus(Guid roleId, CancellationToken cancellationToken = default)
        {
            CheckMenus();
            return await db.RoleMenu.Where(m => m.RoleId == roleId && !m.IsDeleted).Select(m => m.MenuId).ToListAsync(cancellationToken);
        }
        public async Task<int> Insert(MenuState menu, CancellationToken cancellationToken = default) { CheckMenus(); return await db.InsertAsync(mapper.ToMenuEntity(menu)!, token: cancellationToken); }
        public async Task<int> Update(MenuState menu, CancellationToken cancellationToken = default)
        {
            CheckMenus();
            // 显式指定列可防止将来仅供应用程序使用的字段被意外持久化。
            return await db.Menus.Where(m => m.Id == menu.Id && !m.IsDeleted)
                .Set(m => m.DisplayName, menu.DisplayName)
                .Set(m => m.Description, menu.Description).Set(m => m.Icon, menu.Icon)
                .Set(m => m.Url, menu.Destination.Address).Set(m => m.Component, menu.Destination.View)
                .Set(m => m.ControllerName, menu.Destination.Resource).Set(m => m.ActionName, menu.Destination.Operation)
                .Set(m => m.LinkTarget, menu.Destination.OpenMode).Set(m => m.Number, menu.Number)
                .Set(m => m.ParentId, menu.ParentId).Set(m => m.IsDisplay, menu.IsDisplay).Set(m => m.IsAction, menu.IsAction)
                .Set(m => m.UpdatedBy, menu.UpdatedBy).Set(m => m.UpdatedAt, menu.UpdatedAt).UpdateAsync(cancellationToken);
        }
        public async Task<int> Remove(List<int> ids, string userId, DateTime timestamp, CancellationToken cancellationToken = default)
        {
            CheckMenus();
            await db.RoleMenu.Where(m => !m.IsDeleted && ids.Contains(m.MenuId)).Set(m => m.IsDeleted, true)
                .Set(m => m.UpdatedBy, userId).Set(m => m.UpdatedAt, timestamp).UpdateAsync(cancellationToken);
            return await db.Menus.Where(m => !m.IsDeleted && ids.Contains(m.Id)).Set(m => m.IsDeleted, true)
                .Set(m => m.UpdatedBy, userId).Set(m => m.UpdatedAt, timestamp).UpdateAsync(cancellationToken);
        }
        public async Task<bool> ApplyRoleChanges(Guid roleId, List<int> additions, List<int> removals, string userId, DateTime timestamp, CancellationToken cancellationToken = default)
        {
            CheckMenus();
            if (removals.Count > 0 && await db.RoleMenu.Where(m => m.RoleId == roleId && !m.IsDeleted && removals.Contains(m.MenuId))
                .Set(m => m.IsDeleted, true).Set(m => m.UpdatedBy, userId).Set(m => m.UpdatedAt, timestamp).UpdateAsync(cancellationToken) != removals.Count) return false;
            foreach (var id in additions)
                if (await db.InsertAsync(new RoleMenu { RoleId = roleId, MenuId = id, CreatedBy = userId, CreatedAt = timestamp }, token: cancellationToken) != 1) return false;
            return true;
        }
    }
}
