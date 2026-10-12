using System.Collections.Generic;
namespace My.XXX.Domain.Menus.Models;

public sealed class MenuUpdate
{
    public List<MenuOptionalField>? ClearFields { get; set; }
    public int Id { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Address { get; set; }
    public string? View { get; set; }
    public string? Resource { get; set; }
    public string? Operation { get; set; }
    public string? OpenMode { get; set; }
    public int? Number { get; set; }
    public int? ParentId { get; set; }
    public bool? IsDisplay { get; set; }
    public bool? IsAction { get; set; }
}

