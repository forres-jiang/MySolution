using My.XXX.Domain.Authentication.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace My.XXX.Persistence.Mapping;

/// <summary>带版本的身份存储结构，独立于公开响应；反序列化后验证必填字段。</summary>
public sealed class IdentitySnapshot
{
    // 旧数据缺少版本字段时按最初的版本零读取。
    public int Version { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public List<string>? Roles { get; set; }
    public List<Guid>? RoleIds { get; set; }

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
        {
            throw new InvalidOperationException("不支持此身份快照版本。");
        }
        if (string.IsNullOrWhiteSpace(snapshot.UserId) || snapshot.UserId.Length > UserIdentity.MaxUserIdLength)
        {
            throw new InvalidOperationException("身份快照的用户标识为空或超过存储长度限制。");
        }
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
