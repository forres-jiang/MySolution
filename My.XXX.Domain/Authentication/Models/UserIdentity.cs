using System;
using System.Collections.Generic;
using System.Linq;

namespace My.XXX.Domain.Authentication.Models;

/// <summary>独立于导航与 HTTP 响应的不可变身份快照。</summary>
public sealed record UserIdentity
{
    /// <summary>存储列宽上限；写入前的身份校验属于领域规则，不依赖具体数据库。</summary>
    public const int MaxUserIdLength = 200;
    private readonly IReadOnlyList<string> roles = Array.Empty<string>();
    private readonly IReadOnlyList<Guid> roleIds = Array.Empty<Guid>();
    public required string UserId { get; init; }
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public IReadOnlyList<string> Roles { get => roles; init => roles = Array.AsReadOnly(value?.ToArray() ?? Array.Empty<string>()); }
    public IReadOnlyList<Guid> RoleIds { get => roleIds; init => roleIds = Array.AsReadOnly(value?.ToArray() ?? Array.Empty<Guid>()); }

    public bool HasStorableUserId => !string.IsNullOrWhiteSpace(UserId) && UserId.Length <= MaxUserIdLength;
}
