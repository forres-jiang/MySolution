using My.XXX.Application.Authentication.Models;
using My.XXX.Domain.Authentication.Models;
using FluentResults;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Application.Authentication.Ports;

public interface IAuthenticationService
{
    Task<Result<SessionRenewal>> RefreshAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}

public interface ITokenIssuer
{
    SessionTokens Create(UserIdentity user, string sessionId, string refreshTokenId, System.DateTime refreshExpiration);
}
