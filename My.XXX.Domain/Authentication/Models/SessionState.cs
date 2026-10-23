using System;

namespace My.XXX.Domain.Authentication.Models;

public sealed class SessionState
{
    public required string SessionId { get; init; }
    public required string UserId { get; init; }
    public required string RefreshTokenId { get; init; }
    public required DateTime ExpiresUtc { get; init; }

    public bool IsExpired(DateTime nowUtc) => ExpiresUtc <= nowUtc;
}
public sealed class ActiveSession
{
    public required SessionState Session { get; init; }
    public required UserIdentity User { get; init; }
}
