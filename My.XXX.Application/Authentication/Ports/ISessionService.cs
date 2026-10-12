using My.XXX.Application.Authentication.Models;
using My.XXX.Domain.Authentication.Models;
using FluentResults;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Application.Authentication.Ports;

public interface ISessionService
{
    Task<Result<ActiveSession>> ValidateAsync(string sessionId, string? userId, string? refreshTokenId = null, CancellationToken cancellationToken = default);
    Task<Result<SessionTokens>> IssueAsync(string userId, CancellationToken cancellationToken = default);
}
