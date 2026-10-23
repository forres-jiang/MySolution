using System;
using System.Collections.Generic;

namespace My.XXX.Application.Authentication.Models;

/// <summary>应用层签发的令牌对。Web 边界负责映射到刷新令牌响应 DTO。</summary>
public sealed record SessionTokens(string AccessToken, string RefreshToken, int ExpiryInMinutes);

/// <summary>刷新令牌成功的结果。令牌字段在 Web 边界组装进响应 DTO。</summary>
public sealed record SessionRenewal(SessionTokens Tokens);

/// <summary>当前登录用户的只读视图。wire UserInfo 在 Web 边界组装（含空 Menus 以保留旧契约）。</summary>
public sealed class CurrentUserView
{
    public required string UserId { get; init; }
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
    public required IReadOnlyList<Guid> RoleIds { get; init; }
}
