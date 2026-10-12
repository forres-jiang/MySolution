using My.XXX.Application;
using My.XXX.Application.Abstractions.Results;
using My.XXX.Application.Authentication;
using My.XXX.Application.Authentication.Models;
using My.XXX.Application.Authentication.Ports;
using My.XXX.Application.Authorization;
using My.XXX.Application.Menus;
using My.XXX.Domain.Authentication.Models;
using My.XXX.Domain.Authorization.Models;
using My.XXX.Domain.Authorization.Policies;
using My.XXX.Domain.Menus.Models;
using My.XXX.Domain.Menus.Policies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.UnitTests;

[TestClass]
public sealed class CleanArchitectureTests
{
    [TestMethod]
    public async Task AnonymousAndUnprivilegedCallersCannotReachWriteTransactions()
    {
        foreach (var user in new UserIdentity?[] { null, new() { UserId = "reader" } })
        {
            var authorization = AuthorizationTestSupport.Create(user);
            // Null storage makes any accidental access fail this test.
            var results = new[]
            {
                await new MenuCommandService(null!, authorization, TimeProvider.System).Remove(new() { 1 }),
                await new PermissionAdministration(null!, null!, authorization).ReplaceAsync(Guid.NewGuid(), new() { PermissionCodes.MenuGet }),
                await new RoleMenuAssignmentService(null!, authorization, TimeProvider.System).SetRoleMenus(Guid.NewGuid(), new() { 1 }, RoleMenuChange.Add),
                await new RoleAccessAdministration(null!, authorization, TimeProvider.System).ReplaceAsync(Guid.NewGuid(), new() { 1 }, new() { PermissionCodes.MenuGet })
            };
            foreach (var result in results)
                Assert.AreEqual(user == null ? "Authentication.Required" : "Authorization.Forbidden", ((BusinessFailure)result.Errors.Single()).Code);
        }
    }

    [TestMethod]
    public async Task CombinedWriteRequiresBothPermissions()
    {
        var authorization = AuthorizationTestSupport.Create(new UserIdentity { UserId = "writer", RoleIds = new[] { Guid.NewGuid() } }, PermissionCodes.PermissionsWrite);
        var result = await new RoleAccessAdministration(null!, authorization, TimeProvider.System)
            .ReplaceAsync(Guid.NewGuid(), new() { 1 }, new() { PermissionCodes.MenuGet });
        Assert.AreEqual("Authorization.Forbidden", ((BusinessFailure)result.Errors.Single()).Code);
        Assert.IsTrue((await authorization.RequireAnyAsync(default, PermissionCodes.PermissionsWrite)).IsSuccess);
        Assert.IsTrue((await AuthorizationTestSupport.Admin().RequireAnyAsync(default, "unknown")).IsFailed);
    }

    [TestMethod]
    public async Task WhitelistNeverAllowsAnonymousCallers()
    {
        Assert.IsTrue((await AuthorizationTestSupport.Whitelisted(null, PermissionCodes.MenuEdit)
            .RequireAnyAsync(default, PermissionCodes.MenuEdit)).IsFailed);
        Assert.IsTrue((await AuthorizationTestSupport.Whitelisted(new() { UserId = "alice" }, PermissionCodes.MenuEdit)
            .RequireAnyAsync(default, PermissionCodes.MenuEdit)).IsSuccess);
    }

    [TestMethod]
    public void MenuTransitionPreservesSnapshotAndValidatesClearingAndHierarchy()
    {
        var rows = new List<MenuState> { new() { Id = 1, DisplayName = "root", Description = "keep", Icon = "old" }, new() { Id = 2, ParentId = 1 } };
        var now = new DateTime(2026, 1, 1);
        var updated = MenuTransitions.Update(rows, new() { Id = 1, DisplayName = " new ", ClearFields = new() { MenuOptionalField.Icon } }, "alice", now);
        Assert.IsTrue(updated.IsSuccess);
        Assert.AreEqual("new", updated.Value.DisplayName);
        Assert.AreEqual("keep", updated.Value.Description);
        Assert.IsNull(updated.Value.Icon);
        Assert.AreEqual(now, updated.Value.UpdatedAt);
        Assert.AreEqual("old", rows[0].Icon);
        Assert.AreEqual("root", rows[0].DisplayName);
        Assert.IsTrue(MenuTransitions.Update(rows, new() { Id = 1, ParentId = 2 }, "alice", now).IsFailed);
        Assert.IsTrue(MenuTransitions.Update(rows, new() { Id = 1, ClearFields = new() { (MenuOptionalField)99 } }, "alice", now).IsFailed);
        Assert.AreEqual(0, rows[0].ParentId);
    }

    [TestMethod]
    public async Task SessionIssuanceReturnsBusinessFailureAndPersistsSuccessfulSession()
    {
        var store = new IdentityStore();
        var service = new SessionService(store, new Tokens(), TimeProvider.System, new SessionLifetime(60));
        var missing = await service.IssueAsync("missing");
        Assert.AreEqual("Authentication.UserUnavailable", ((BusinessFailure)missing.Errors.Single()).Code);
        Assert.IsNull(store.Saved);
        store.User = new UserIdentity { UserId = "alice" };
        var issued = await service.IssueAsync("alice");
        Assert.IsTrue(issued.IsSuccess);
        Assert.AreEqual("access", issued.Value.AccessToken);
        Assert.AreEqual("alice", store.Saved!.UserId);
    }

    [TestMethod]
    public void SessionLifetimeIsValidatedThroughRegisteredOptions()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        using var provider = services.BuildServiceProvider();
        Assert.ThrowsExactly<OptionsValidationException>(() => { _ = provider.GetRequiredService<IOptions<SessionOptions>>().Value; });
    }

    private sealed class Tokens : ITokenIssuer
    {
        public SessionTokens Create(UserIdentity user, string sessionId, string tokenId, DateTime expiry) => new("access", "refresh", 5);
    }
    private sealed class IdentityStore : IAuthenticationStore
    {
        public UserIdentity? User;
        public SessionState? Saved;
        public Task<UserIdentity?> GetUserAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(User);
        public Task CreateSessionAsync(SessionState session, CancellationToken cancellationToken = default) { Saved = session; return Task.CompletedTask; }
        public Task SetUserAsync(UserIdentity user, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ActiveSession?> GetActiveSessionAsync(string id, DateTime now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RotateAsync(string id, string expected, string next, DateTime now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
