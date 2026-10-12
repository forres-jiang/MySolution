using LinqToDB;
using LinqToDB.Mapping;
using System;

namespace My.XXX.Persistence.PersistenceModels;

[Table(Schema = "dbo", Name = "Menus")]
[Table(Configuration = ProviderName.PostgreSQL, Schema = "public", Name = "Menus")]
public class Menus
{
    [PrimaryKey, Identity]
    public int Id { get; set; }

    [Column, Nullable]
    public string? DisplayName { get; set; }

    [Column, Nullable]
    public string? Description { get; set; }

    [Column, Nullable]
    public string? Icon { get; set; }

    [Column, NotNull]
    public int Number { get; set; }

    [Column, Nullable]
    public string? Url { get; set; }

    /// <summary>
    /// 前端组件路基
    /// </summary>
    [Column, Nullable]
    public string? Component { get; set; }

    [Column, Nullable]
    public string? ControllerName { get; set; }

    [Column, Nullable]
    public string? ActionName { get; set; }

    [Column, Nullable]
    public string? LinkTarget { get; set; }

    [Column, NotNull]
    public int ParentId { get; set; }

    [Column, NotNull]
    public bool IsDisplay { get; set; }

    [Column, NotNull]
    public bool IsAction { get; set; }

    [Column, NotNull]
    public bool IsDeleted { get; set; }

    [Column, NotNull]
    public required string CreatedBy { get; set; }

    [Column, NotNull]
    public DateTime CreatedAt { get; set; }

    [Column, Nullable]
    public string? UpdatedBy { get; set; }

    [Column, Nullable]
    public DateTime? UpdatedAt { get; set; }
}
