using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace My.XXX.Infrastructure.Caching;

public static class PermissionCacheKey
{
    // Separate namespace: this value represents only the role projection, never a user's final permissions.
    public static string CreateRoleSet(string prefix, IEnumerable<Guid> roleIds, long revision) =>
        Create(prefix + ":roles:v1", "role-projection", roleIds, revision);

    public static string Create(string prefix, string userId, IEnumerable<Guid> roleIds, long revision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var roles = string.Join(",", roleIds.Distinct().OrderBy(id => id).Select(id => id.ToString("N")));
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId + "\n" + roles)));
        return prefix + ":" + revision.ToString(CultureInfo.InvariantCulture) + ":" + identity;
    }
}
