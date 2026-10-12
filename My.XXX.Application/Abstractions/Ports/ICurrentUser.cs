using My.XXX.Domain.Authentication.Models;

namespace My.XXX.Application.Abstractions.Ports;

public interface ICurrentUser
{
    UserIdentity? User { get; }
}
