using My.XXX.Application.Abstractions.Ports;
using My.XXX.Application.Authorization;
using My.XXX.Application.Authorization.Ports;
using My.XXX.Domain.Authentication.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.UnitTests;

internal static class AuthorizationTestSupport
{
    public static UseCaseAuthorization Admin() => Create(new UserIdentity { UserId = "alice", Roles = new[] { "AppAdmin" } });
    public static UseCaseAuthorization Create(UserIdentity? user, params string[] permissions) =>
        new(new Current(user), new Grants(permissions), new Monitor());
    public static UseCaseAuthorization Whitelisted(UserIdentity? user, string code) =>
        new(new Current(user), new Grants(Array.Empty<string>()), new Monitor(new() { Codes = new() { code } }));
    private sealed class Current(UserIdentity? user) : ICurrentUser { public UserIdentity? User => user; }
    private sealed class Grants(string[] codes) : IPermissionQuery
    {
        public Task<List<string>> GetPermissionCodesAsync(List<Guid> roles, string userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<string>(codes));
    }
    private sealed class Monitor(PermissionAccessOptions? options = null) : IOptionsMonitor<PermissionAccessOptions>
    {
        public PermissionAccessOptions CurrentValue { get; } = options ?? new();
        public PermissionAccessOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<PermissionAccessOptions, string?> listener) => null;
    }
}
