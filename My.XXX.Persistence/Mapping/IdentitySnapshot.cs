using My.XXX.Domain.Authentication.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace My.XXX.Persistence.Mapping;

/// <summary>Versioned storage schema, independent of the public user response shape.</summary>
public sealed class IdentitySnapshot
{
    // Missing in legacy rows; zero means the original unversioned snapshot.
    public int Version { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<Guid> RoleIds { get; set; } = new();

    public static string Write(UserIdentity user) => JsonConvert.SerializeObject(new IdentitySnapshot
    {
        Version = 1,
        UserId = user.UserId,
        UserName = user.UserName,
        Email = user.Email,
        Roles = new(user.Roles),
        RoleIds = new(user.RoleIds)
    });

    public static UserIdentity Read(string json)
    {
        var snapshot = JsonConvert.DeserializeObject<IdentitySnapshot>(json)
            ?? throw new InvalidOperationException("Identity snapshot is empty.");
        if (snapshot.Version is not (0 or 1))
            throw new InvalidOperationException("Unsupported identity snapshot version.");
        if (string.IsNullOrWhiteSpace(snapshot.UserId))
            throw new InvalidOperationException("Identity snapshot has no user ID.");
        return new UserIdentity
        {
            UserId = snapshot.UserId,
            UserName = snapshot.UserName,
            Email = snapshot.Email,
            Roles = snapshot.Roles ?? new(),
            RoleIds = snapshot.RoleIds ?? new()
        };
    }
}
