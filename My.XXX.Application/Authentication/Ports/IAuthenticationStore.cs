using My.XXX.Domain.Authentication.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
namespace My.XXX.Application.Authentication.Ports;
/// <summary>共享的持久化权限存储。仅受信任的身份集成可以更新用户。</summary>
public interface IAuthenticationStore
{
    Task SetUserAsync(UserIdentity user, bool enabled, CancellationToken cancellationToken = default);
    Task<UserIdentity?> GetUserAsync(string userId, CancellationToken cancellationToken = default);
    Task CreateSessionAsync(SessionState session, CancellationToken cancellationToken = default);
    Task<ActiveSession?> GetActiveSessionAsync(string sessionId, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<bool> RotateAsync(string sessionId, string expectedTokenId, string nextTokenId, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task RevokeAsync(string sessionId, CancellationToken cancellationToken = default);
}
