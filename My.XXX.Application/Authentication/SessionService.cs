using My.XXX.Application.Abstractions.Results;
using My.XXX.Application.Authentication.Models;
using My.XXX.Application.Authentication.Ports;
using My.XXX.Domain.Abstractions;
using My.XXX.Domain.Authentication.Models;
using FluentResults;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Application.Authentication;

public sealed class SessionService(IAuthenticationStore store, ITokenIssuer tokens,
    TimeProvider clock, SessionLifetime lifetime) : ISessionService
{
    public async Task<Result<ActiveSession>> ValidateAsync(string sessionId, string? userId,
        string? refreshTokenId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(userId))
            return Result.Fail<ActiveSession>(AuthenticationErrors.InvalidToken().ToError());
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var active = await store.GetActiveSessionAsync(sessionId, nowUtc, cancellationToken);
        // Store already filters expiry; re-assert on the domain object so the security invariant does not depend solely on the persistence query.
        if (active == null || active.Session.IsExpired(nowUtc) || active.User.UserId != userId ||
            (refreshTokenId != null && active.Session.RefreshTokenId != refreshTokenId))
            return Result.Fail<ActiveSession>(AuthenticationErrors.InvalidToken().ToError());
        return Result.Ok(active);
    }

    public async Task<Result<SessionTokens>> IssueAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Result.Fail<SessionTokens>(AuthenticationErrors.UserUnavailable().ToError());
        var user = await store.GetUserAsync(userId, cancellationToken);
        if (user == null) return Result.Fail<SessionTokens>(AuthenticationErrors.UserUnavailable().ToError());
        var session = new SessionState
        {
            SessionId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            RefreshTokenId = Guid.NewGuid().ToString("N"),
            ExpiresUtc = clock.GetUtcNow().UtcDateTime.Add(lifetime.Duration)
        };
        var pair = tokens.Create(user, session.SessionId, session.RefreshTokenId, session.ExpiresUtc);
        await store.CreateSessionAsync(session, cancellationToken);
        return Result.Ok(pair);
    }
}

public static class AuthenticationErrors
{
    public static BusinessError UserUnavailable() => new("User unavailable.", "Authentication.UserUnavailable", BusinessErrorKind.Unauthorized);
    public static BusinessError InvalidToken() => new("Token invalid.", code: "Authentication.InvalidToken", kind: BusinessErrorKind.Unauthorized);
    public static BusinessError RefreshRejected() => new("Refresh token has already been used or revoked.", code: "Authentication.RefreshRejected", kind: BusinessErrorKind.Unauthorized);
}
