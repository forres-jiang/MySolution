using LinqToDB;
using LinqToDB.Mapping;

namespace My.XXX.Persistence.PersistenceModels;

[Table(Schema = "dbo", Name = "MenuWriteLock")]
[Table(Configuration = ProviderName.PostgreSQL, Schema = "public", Name = "MenuWriteLock")]
public sealed class MenuWriteLock
{
    [PrimaryKey, Column]
    public int Id { get; set; }

    [Column, NotNull]
    public long Version { get; set; }
}
