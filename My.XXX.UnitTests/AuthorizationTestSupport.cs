using My.XXX.Application.Abstractions.Ports;
using My.XXX.Application.Authorization;
using My.XXX.Application.Authorization.Ports;
using My.XXX.Domain.Authentication.Models;
using My.XXX.Domain.Authorization.Policies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.UnitTests;

internal static class AuthorizationTestSupport
{
    // 授予全部权限码的已授权用户，用于验证业务逻辑而非鉴权分支。
    public static UseCaseAuthorization Admin() =>
        Create(new UserIdentity { UserId = "alice", RoleIds = new[] { Guid.NewGuid() } }, PermissionCodes.All.ToArray());
    public static UseCaseAuthorization Create(UserIdentity? user, params string[] permissions) =>
        new(new Current(user), new Grants(permissions));
    private sealed class Current(UserIdentity? user) : ICurrentUser { public UserIdentity? User => user; }
    private sealed class Grants(string[] codes) : IPermissionQuery
    {
        public Task<List<string>> GetPermissionCodesAsync(List<Guid> roles, string userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<string>(codes));
    }
}
