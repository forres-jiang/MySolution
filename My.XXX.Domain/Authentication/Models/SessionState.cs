using System;

namespace My.XXX.Domain.Authentication.Models;

public sealed class SessionState
{
    public string SessionId { get; init; }
    public string UserId { get; init; }
    public string RefreshTokenId { get; init; }
    public DateTime ExpiresUtc { get; init; }

    public bool IsExpired(DateTime nowUtc) => ExpiresUtc <= nowUtc;
}
public sealed class ActiveSession
{
    public SessionState Session { get; init; }
    public UserIdentity User { get; init; }
}
