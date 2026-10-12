using My.XXX.Application.Authentication.Models;
using My.XXX.Application.Authentication.Ports;
using System;

namespace My.XXX.Application.Authentication
{
    public class UserService : IUserService
    {
        private readonly IAuthenticationSession _currentRequest;

        public UserService(IAuthenticationSession currentRequest)
        {
            _currentRequest = currentRequest;
        }

        public CurrentUserView? CurrentUser => _currentRequest.User is { } user ? new CurrentUserView
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Email = user.Email,
            Roles = user.Roles,
            RoleIds = user.RoleIds
        } : null;
        public DateTime GetTokenExpirationTime() => _currentRequest.TokenExpirationTime;
    }
}
