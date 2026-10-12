using LinqToDB;
using LinqToDB.Mapping;

namespace My.XXX.Persistence.PersistenceModels;

[Table(Schema = "dbo", Name = "AuthenticationUsers")]
[Table(Configuration = ProviderName.PostgreSQL, Schema = "public", Name = "AuthenticationUsers")]
public sealed class AuthenticationUser
{
    [PrimaryKey, Column, NotNull]
    public string? UserId { get; set; }

    [Column, NotNull]
    public string? UserJson { get; set; }

    [Column]
    public bool Enabled { get; set; }
}
