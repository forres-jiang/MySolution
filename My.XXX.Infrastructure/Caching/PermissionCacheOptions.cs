namespace My.XXX.Infrastructure.Caching;

public sealed class PermissionCacheOptions
{
    public string KeyPrefix { get; set; } = "My.XXX:permissions:v3";
    public int ExpiryInMinutes { get; set; } = 5;
}

