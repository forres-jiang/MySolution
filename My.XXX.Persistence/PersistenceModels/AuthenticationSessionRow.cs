using LinqToDB;
using LinqToDB.Mapping;
using System;

namespace My.XXX.Persistence.PersistenceModels;

[Table(Schema = "dbo", Name = "AuthenticationSessions")]
[Table(Configuration = ProviderName.PostgreSQL, Schema = "public", Name = "AuthenticationSessions")]
public sealed class AuthenticationSessionRow
{
    [PrimaryKey, Column, NotNull]
    public string? SessionId { get; set; }

    [Column, NotNull]
    public string? UserId { get; set; }

    [Column, NotNull]
    public string? RefreshTokenId { get; set; }

    [Column]
    public DateTime ExpiresUtc { get; set; }

    [Column]
    public bool Revoked { get; set; }
}
