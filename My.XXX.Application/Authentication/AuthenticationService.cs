using My.XXX.Application.Abstractions.Results;
using My.XXX.Application.Authentication.Models;
using My.XXX.Application.Authentication.Ports;
using FluentResults;
using System;
using System.Threading;
using System.Threading.Tasks;
namespace My.XXX.Application.Authentication;

public sealed class AuthenticationService(IAuthenticationSession current, ITokenIssuer tokens,
    IAuthenticationStore store, TimeProvider clock, ISessionService sessions) : IAuthenticationService
{
    public async Task<Result<SessionRenewal>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!current.IsAuthenticated || string.IsNullOrEmpty(current.SessionId) || string.IsNullOrEmpty(current.TokenId))
            return Result.Fail<SessionRenewal>(AuthenticationErrors.InvalidToken().ToError());
        var validation = await sessions.ValidateAsync(current.SessionId, current.User?.UserId, current.TokenId, cancellationToken);
        if (validation.IsFailed) return Result.Fail<SessionRenewal>(validation.Errors);
        var active = validation.Value;
        var nextId = Guid.NewGuid().ToString("N");
        var pair = tokens.Create(active.User, current.SessionId, nextId, active.Session.ExpiresUtc);
        if (!await store.RotateAsync(current.SessionId, current.TokenId, nextId, clock.GetUtcNow().UtcDateTime, cancellationToken))
            return Result.Fail<SessionRenewal>(AuthenticationErrors.RefreshRejected().ToError());
        return Result.Ok(new SessionRenewal(pair));
    }
    public Task LogoutAsync(CancellationToken cancellationToken = default) => string.IsNullOrEmpty(current.SessionId)
        ? Task.CompletedTask : store.RevokeAsync(current.SessionId, cancellationToken);
}
