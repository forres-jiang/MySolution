using System.Collections.Frozen;
using System.Collections.Generic;
namespace My.XXX.Domain.Authorization.Policies;

/// <summary>稳定的安全标识符。导航标签、路由和 CLR 名称都不是权限。</summary>
public static class PermissionCodes
{
    public const string MenuAdd = "menu.add", MenuRemove = "menu.remove", MenuEdit = "menu.edit", MenuGet = "menu.get",
        MenuList = "menu.list", MenuSearch = "menu.search", MenuRoleMenu = "menu.role-menu",
        MenuRoleMenus = "menu.role-menus", MenuRoleMenuChecked = "menu.role-menu-checked",
        MenuRemoveRoleMenu = "menu.remove-role-menu", MenuTree = "menu.tree", MenuDisplayTree = "menu.display-tree",
        MenuTreeByRoleId = "menu.tree-by-role-id", MenuTreeByRoleIds = "menu.tree-by-role-ids",
        MenuUpdateSort = "menu.update-sort", OperationList = "operation.list",
        PermissionsRead = "permissions.read", PermissionsWrite = "permissions.write";
    public static IReadOnlyList<string> All { get; } = System.Array.AsReadOnly(new[] {
        MenuAdd, MenuRemove, MenuEdit, MenuGet, MenuList, MenuSearch, MenuRoleMenu, MenuRoleMenus, MenuRoleMenuChecked,
        MenuRemoveRoleMenu, MenuTree, MenuDisplayTree, MenuTreeByRoleId, MenuTreeByRoleIds, MenuUpdateSort,
        OperationList, PermissionsRead, PermissionsWrite });

    /// <summary>用于 O(1) 成员校验；<see cref="All"/> 保留列表形态供目录接口返回。</summary>
    public static IReadOnlySet<string> AllSet { get; } = All.ToFrozenSet(System.StringComparer.Ordinal);
}
