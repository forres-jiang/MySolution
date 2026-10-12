using My.XXX.Application.Abstractions.Ports;
using System;

namespace My.XXX.Application.Authentication.Ports;

public interface IAuthenticationSession : ICurrentUser
{
    bool IsAuthenticated { get; }
    DateTime TokenExpirationTime { get; }
    string? SessionId { get; }
    string? TokenId { get; }
}
