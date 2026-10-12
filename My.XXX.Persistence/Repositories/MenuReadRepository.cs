using My.XXX.Application.Abstractions;
using My.XXX.Application.Menus.Models;
using My.XXX.Application.Menus.Ports;
using My.XXX.Domain.Menus.Models;
using My.XXX.Persistence.Mapping;
using My.XXX.Persistence.PersistenceModels;
using LinqToDB;
using LinqToDB.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Persistence.Repositories;

public sealed class MenuReadRepository(DBContext db) : IMenuReadRepository
{
    private readonly PersistenceMapper mapper = new();
    private IQueryable<Menus> RoleMenus(List<Guid> roleIds) =>
        (from rm in db.RoleMenu
         join m in db.Menus on rm.MenuId equals m.Id
         where roleIds.Contains(rm.RoleId) && !rm.IsDeleted && !m.IsDeleted
         select m).Distinct();
    public async Task<MenuState?> Get(int id, CancellationToken cancellationToken = default) => mapper.ToMenuState(await db.Menus.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, cancellationToken));
    public async Task<List<MenuState>> GetMenus(int? parentId = null, List<int>? ids = null, bool? isDisplay = null, CancellationToken cancellationToken = default)
    {
        var query = db.Menus.Where(m => !m.IsDeleted);
        if (parentId.HasValue) query = query.Where(m => m.ParentId == parentId.Value);
        if (ids != null) query = query.Where(m => ids.Contains(m.Id));
        if (isDisplay.HasValue) query = query.Where(m => m.IsDisplay == isDisplay.Value);
        return mapper.ToMenuStates(await query.ToListAsync(cancellationToken));
    }
    public async Task<List<MenuState>> GetRoleMenuByRoles(List<Guid> roleIds, CancellationToken cancellationToken = default) => mapper.ToMenuStates(await RoleMenus(roleIds).ToListAsync(cancellationToken));
    public async Task<Paged<MenuState>> Search(MenuSearch query, CancellationToken cancellationToken = default)
    {
        var menus = db.Menus.Where(m => !m.IsDeleted);
        if (query.IsAction.HasValue) menus = menus.Where(m => m.IsAction == query.IsAction.Value);
        if (query.IsDisplay) menus = menus.Where(m => m.IsDisplay);
        if (!string.IsNullOrEmpty(query.DisplayName)) menus = menus.Where(m => m.DisplayName != null && m.DisplayName.Contains(query.DisplayName));
        if (query.ParentId.HasValue) menus = menus.Where(m => m.ParentId == query.ParentId.Value);
        var total = await menus.CountAsync(cancellationToken);
        var ordered = query.Sort == MenuSort.ParentThenNumber
            ? menus.OrderBy(m => m.ParentId).ThenBy(m => m.Number).ThenBy(m => m.Id)
            : menus.OrderByDescending(m => m.CreatedAt).ThenBy(m => m.Id);
        var list = await ordered
            .Skip(query.PageIndex * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return Paged<MenuState>.Create(mapper.ToMenuStates(list), total);
    }

}
