using System;
using System.Collections.Generic;

namespace My.XXX.Application.Menus.Models;

/// <summary>新增菜单的应用层输入；wire 契约映射由组合根完成。</summary>
public sealed class MenuCreation
{
    public required string DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Url { get; set; }
    public string? Component { get; set; }
    public string? ControllerName { get; set; }
    public string? ActionName { get; set; }
    public string? LinkTarget { get; set; }
    public int Number { get; set; }
    public int ParentId { get; set; }
    public bool IsDisplay { get; set; }
    public bool? IsAction { get; set; }
}

/// <summary>部分更新菜单的应用层输入；null 值保留原数据，ClearFields 显式清空。</summary>
public sealed class MenuModification
{
    public List<string>? ClearFields { get; set; }
    public int Id { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Url { get; set; }
    public string? Component { get; set; }
    public string? ControllerName { get; set; }
    public string? ActionName { get; set; }
    public string? LinkTarget { get; set; }
    public int? Number { get; set; }
    public int? ParentId { get; set; }
    public bool? IsDisplay { get; set; }
    public bool? IsAction { get; set; }
}

public sealed record MenuListingQuery(string DisplayName, bool? IsAction, bool IsDisplay, int? ParentId, int PageIndex, int PageSize);

/// <summary>菜单查询的统一应用层输出；树形结构与勾选状态在应用层组装，序列化留给输出边界。</summary>
public sealed class MenuView
{
    public int Id { get; set; }
    public required string DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int Number { get; set; }
    public string? Url { get; set; }
    public string? Component { get; set; }
    public string? ControllerName { get; set; }
    public string? ActionName { get; set; }
    public string? LinkTarget { get; set; }
    public int ParentId { get; set; }
    public bool IsDisplay { get; set; }
    public bool IsAction { get; set; }
    public bool IsDeleted { get; set; }
    public required string CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool Checked { get; set; }
    public List<MenuView>? Actions { get; set; }
    public List<MenuView>? Children { get; set; }
}
