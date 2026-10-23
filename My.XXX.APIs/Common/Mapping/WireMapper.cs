using My.XXX.APIs.Contracts.Authentication;
using My.XXX.APIs.Contracts.Menus;
using My.XXX.Application.Authentication.Models;
using My.XXX.Application.Menus.Models;
using My.XXX.Domain.Menus.Models;
using Riok.Mapperly.Abstractions;
using System;
using System.Collections.Generic;

namespace My.XXX.APIs.Common.Mapping;

/// <summary>HTTP 契约（Contracts DTO）与应用层模型之间的边界映射，仅存在于组合根。</summary>
[Mapper(ThrowOnMappingNullMismatch = false, ThrowOnPropertyMappingNullMismatch = false)]
public sealed partial class WireMapper
{
    public static readonly WireMapper Instance = new();

    public UserInfoResponse? ToUserInfoResponse(CurrentUserView? view) => view == null ? null : new UserInfoResponse
    {
        UserId = view.UserId,
        UserName = view.UserName ?? string.Empty,
        Email = view.Email ?? string.Empty,
        Roles = new List<string>(view.Roles),
        RoleIds = new List<Guid>(view.RoleIds),
        // 旧契约：GetRoles 响应始终携带空 Menus 数组。
        Menus = new List<MenuTreeResponse>()
    };

    public partial MenuCreation? ToMenuCreation(CreateMenuRequest? model);

    public partial MenuModification? ToMenuModification(UpdateMenuRequest? model);

    public MenuReorderCommand ToReorderCommand(ReorderMenuRequest model) => new(model.CurrentId, model.PrevId, model.NextId);

    public MenuListingQuery ToListingQuery(MenuQuery query) =>
        new(query.DisplayName, query.IsAction, query.IsDisplay, query.ParentId, query.PageIndex, query.PageSize);

    public partial MenuTreeResponse? ToMenuTreeResponse(MenuView? view);
    public partial List<MenuTreeResponse> ToMenuTreeResponses(IEnumerable<MenuView>? views);

    [MapperIgnoreSource(nameof(MenuView.Checked))]
    [MapperIgnoreSource(nameof(MenuView.Actions))]
    [MapperIgnoreSource(nameof(MenuView.Children))]
    public partial MenuResponse? ToMenuResponse(MenuView? view);
    public partial List<MenuResponse> ToMenuResponses(IEnumerable<MenuView>? views);

    [MapperIgnoreSource(nameof(MenuView.Checked))]
    [MapperIgnoreSource(nameof(MenuView.Actions))]
    [MapperIgnoreSource(nameof(MenuView.Children))]
    public partial MenuSearchPickerResponse? ToMenuSearchPicker(MenuView? view);
    public partial List<MenuSearchPickerResponse> ToMenuSearchPickers(IEnumerable<MenuView>? views);
}
