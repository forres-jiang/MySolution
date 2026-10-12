using My.XXX.APIs.Contracts.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace My.XXX.APIs.Contracts.Menus;

public class MenuResponseBase
{
    public int Id { get; set; }
    public string DisplayName { get; set; }
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
    public string CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MenuResponse
{
    public int Id { get; set; }
    public string DisplayName { get; set; }
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
    public string CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 用于前台菜单和用户登录成功
/// </summary>
public class MenuTreeResponse : MenuResponse
{
    public bool Checked { get; set; }
    public List<MenuTreeResponse>? Actions { get; set; }
    public List<MenuTreeResponse>? Children { get; set; }
}

public class MenuSearchPickerResponse : MenuResponse
{
    public int Value => Id;
}

#region Input

public class MenuRequestBase
{
    [DataType(DataType.Text)]
    public string? Description { get; set; }

    [DataType(DataType.Text)]
    public string? Icon { get; set; }

    [DataType(DataType.Text)]
    public string? Url { get; set; }

    [DataType(DataType.Text)]
    public string? Component { get; set; }

    [DataType(DataType.Text)]
    public string? ControllerName { get; set; }

    [DataType(DataType.Text)]
    public string? ActionName { get; set; }

    [DataType(DataType.Text)]
    public string? LinkTarget { get; set; }
}

public class CreateMenuRequest : MenuRequestBase
{
    [Required]
    [DataType(DataType.Text)]
    public string DisplayName { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    [Required]
    public int Number { get; set; }

    [Required]
    public int ParentId { get; set; }

    public bool IsDisplay { get; set; }

    [Required(ErrorMessage = "The IsAction field is required.")]
    public bool? IsAction { get; set; }
}

public class RemoveMenusRequest
{
    public List<int> MenuIds { get; set; }
}

public class UpdateMenuRequest : MenuRequestBase
{
    [DataType(DataType.Text)]
    public string? DisplayName { get; set; }

    // null 或未提供的值将保留原有数据。此处列出的字段名将显式清空对应的可空字符串。
    public List<string>? ClearFields { get; set; }

    public int Id { get; set; }

    public int? Number { get; set; }

    public int? ParentId { get; set; }

    public bool? IsDisplay { get; set; }

    public bool? IsAction { get; set; }
}

#endregion Input

#region Query

public class MenuQuery : PaginationQuery
{
    [DataType(DataType.Text)]
    public string DisplayName { get; set; }

    public bool? IsAction { get; set; }

    public bool IsDisplay { get; set; }

    public int? ParentId { get; set; }
}

#endregion Query

public class MenuNavigationResponse
{
    public int Id { get; set; }

    public string DisplayName { get; set; }

    public string Label { get; set; }

    public int Number { get; set; }

    public string Url { get; set; }

    public string LinkTarget { get; set; }

    public int ParentId { get; set; }

    public bool IsDisplay { get; set; }

    public string Icon { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual List<MenuNavigationResponse> Children { get; set; }
}

public class ReorderMenuRequest
{
    [Range(1, int.MaxValue)]
    public int CurrentId { get; set; }

    public int PrevId { get; set; }
    public int NextId { get; set; }
}
