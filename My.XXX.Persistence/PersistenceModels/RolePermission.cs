using LinqToDB;
using LinqToDB.Mapping;
using System;

namespace My.XXX.Persistence.PersistenceModels;

[Table(Schema = "dbo", Name = "RolePermissions")]
[Table(Configuration = ProviderName.PostgreSQL, Schema = "public", Name = "RolePermissions")]
public sealed class RolePermission
{
    [PrimaryKey(1), Column]
    public Guid RoleId { get; set; }

    [PrimaryKey(2), Column, NotNull]
    public required string Code { get; set; }
}
