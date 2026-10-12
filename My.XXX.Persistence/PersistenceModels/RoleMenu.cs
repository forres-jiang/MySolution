using LinqToDB;
using LinqToDB.Mapping;
using System;

namespace My.XXX.Persistence.PersistenceModels;

[Table(Schema = "dbo", Name = "RoleMenu")]
[Table(Configuration = ProviderName.PostgreSQL, Schema = "public", Name = "RoleMenu")]
public class RoleMenu
{
    [PrimaryKey, Identity]
    public int Id { get; set; }

    [Column, NotNull]
    public Guid RoleId { get; set; }

    [Column, NotNull]
    public int MenuId { get; set; }

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
