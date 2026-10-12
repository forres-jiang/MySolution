using My.XXX.Application.Authentication.Models;
using System;

namespace My.XXX.Application.Authentication.Ports
{
    public interface IUserService
    {
        public CurrentUserView? CurrentUser { get; }

        public DateTime GetTokenExpirationTime();
    }
}
