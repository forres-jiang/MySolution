using My.XXX.Application.Abstractions;
using My.XXX.Application.Abstractions.Results;
using My.XXX.Application.Authorization;
using My.XXX.Application.Menus.Mapping;
using My.XXX.Application.Menus.Models;
using My.XXX.Application.Menus.Ports;
using My.XXX.Application.Menus.Queries;
using My.XXX.Domain.Authorization.Policies;
using My.XXX.Domain.Menus.Policies;
using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace My.XXX.Application.Menus;

public sealed class MenuQueryService(IMenuReadRepository _menuRepository, ApplicationMapper _mapper, UseCaseAuthorization authorization)
{
    public async Task<Result<MenuView>> FindAsync(int menuId, CancellationToken cancellationToken = default)
    {
        var access = await authorization.RequireAnyAsync(cancellationToken, PermissionCodes.MenuGet);
        if (access.IsFailed) return Result.Fail<MenuView>(access.Errors);
        var menu = (await _menuRepository.Get(menuId, cancellationToken));
        if (menu == null) return Result.Fail<MenuView>(MenuErrors.NotFound().ToError());
        return Result.Ok(_mapper.ToMenuView(menu));
    }

    public Task<Result<List<MenuView>>> GetMenuTreeCheckedByRole(Guid roleId, CancellationToken cancellationToken = default) =>
        GetCheckedTree(new() { roleId }, PermissionCodes.MenuTreeByRoleId, cancellationToken);

    public Task<Result<List<MenuView>>> GetMenuTreeCheckedByRoles(List<Guid> roleIds, CancellationToken cancellationToken = default) =>
        GetCheckedTree(roleIds, PermissionCodes.MenuTreeByRoleIds, cancellationToken);

    private async Task<Result<List<MenuView>>> GetCheckedTree(List<Guid> roleIds, string permission, CancellationToken cancellationToken)
    {
        var access = await authorization.RequireAnyAsync(cancellationToken, permission);
        if (access.IsFailed) return Result.Fail<List<MenuView>>(access.Errors);
        var menus = await _menuRepository.GetMenus(cancellationToken: cancellationToken);
        var selected = (await _menuRepository.GetRoleMenuByRoles(roleIds, cancellationToken)).Select(m => m.Id).ToHashSet();
        var nodes = menus.Select(menu => new MenuNode(menu) { Checked = selected.Contains(menu.Id) }).ToList();
        return Result.Ok(_mapper.ToTreeViews(MenuTree.Build(nodes)));
    }

    public async Task<Result<List<MenuView>>> GetTreeMenus(bool? isDisplay, CancellationToken cancellationToken = default)
    {
        var access = await authorization.RequireAnyAsync(cancellationToken, isDisplay == true ? PermissionCodes.MenuDisplayTree : PermissionCodes.MenuTree);
        if (access.IsFailed) return Result.Fail<List<MenuView>>(access.Errors);
        var menus = await _menuRepository.GetMenus(isDisplay: isDisplay, cancellationToken: cancellationToken);
        return Result.Ok(_mapper.ToTreeViews(MenuTree.Build(menus.Select(menu => new MenuNode(menu)).ToList())));
    }

    public async Task<Result<Paged<MenuView>>> SearchMenus(MenuListingQuery query, CancellationToken cancellationToken = default) =>
        await Search(query, picker: true, cancellationToken);

    public async Task<Result<Paged<MenuView>>> GetMenus(MenuListingQuery query, CancellationToken cancellationToken = default) =>
        await Search(query, picker: false, cancellationToken);

    private async Task<Result<Paged<MenuView>>> Search(MenuListingQuery query, bool picker, CancellationToken cancellationToken = default)
    {
        var access = await authorization.RequireAnyAsync(cancellationToken, picker ? PermissionCodes.MenuSearch : PermissionCodes.MenuList);
        if (access.IsFailed) return Result.Fail<Paged<MenuView>>(access.Errors);
        var paging = My.XXX.Application.Abstractions.Models.PageWindow.FromRequest(query.PageIndex, query.PageSize);
        var criteria = new MenuSearch(query.DisplayName, picker ? false : query.IsAction,
            picker || query.IsDisplay, query.ParentId, paging.PageIndex, paging.Size,
            picker ? MenuSort.ParentThenNumber : MenuSort.RecentFirst);
        var page = (await _menuRepository.Search(criteria, cancellationToken));
        var result = page.List.Select(menu => _mapper.ToMenuView(menu)).ToList();
        return Result.Ok(Paged<MenuView>.Create(result, page.Total));
    }
}
