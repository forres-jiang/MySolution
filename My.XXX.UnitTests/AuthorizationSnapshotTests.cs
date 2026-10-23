using My.XXX.Application.Abstractions.Ports;
using My.XXX.Application.AccessControl.Ports;
using My.XXX.Application.Authorization;
using My.XXX.Application.Authorization.Ports;
using My.XXX.Domain.Authentication.Models;
using My.XXX.Domain.Authorization.Policies;
using My.XXX.Domain.Menus.Models;
using FluentResults;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.UnitTests;

[TestClass]
public sealed class AuthorizationSnapshotTests
{
    [TestMethod]
    public async Task RepeatedAndConcurrentChecksShareOnlyTheCurrentIdentitySnapshot()
    {
        var firstRole = Guid.NewGuid();
        var secondRole = Guid.NewGuid();
        var current = new CurrentUser { User = new UserIdentity { UserId = "alice", RoleIds = new[] { firstRole, secondRole } } };
        var query = new PermissionQueryStub();
        using var authorization = new UseCaseAuthorization(current, query);

        var checks = await Task.WhenAll(
            authorization.RequireAnyAsync(default, PermissionCodes.MenuGet),
            authorization.RequireAnyAsync(default, PermissionCodes.PermissionsWrite));
        Assert.IsTrue(Array.TrueForAll(checks, result => result.IsSuccess));
        Assert.AreEqual(1, query.Reads);

        current.User = current.User with { RoleIds = new[] { secondRole, firstRole, firstRole } };
        Assert.IsTrue((await authorization.RequireAnyAsync(default, PermissionCodes.MenuGet)).IsSuccess);
        Assert.AreEqual(1, query.Reads);

        current.User = current.User with { UserId = "bob" };
        await authorization.RequireAnyAsync(default, PermissionCodes.MenuGet);
        current.User = current.User with { RoleIds = new[] { firstRole } };
        await authorization.RequireAnyAsync(default, PermissionCodes.MenuGet);
        Assert.AreEqual(3, query.Reads);

        current.User = current.User with { RoleIds = Array.Empty<Guid>() };
        Assert.IsTrue((await authorization.RequireAnyAsync(default, PermissionCodes.MenuGet)).IsFailed);
        current.User = current.User with { RoleIds = new[] { firstRole } };
        await authorization.RequireAnyAsync(default, PermissionCodes.MenuGet);
        Assert.AreEqual(4, query.Reads);

        using var nextScope = new UseCaseAuthorization(current, query);
        await nextScope.RequireAnyAsync(default, PermissionCodes.MenuGet);
        Assert.AreEqual(5, query.Reads);
    }

    [TestMethod]
    public async Task FailedReadsAreRetriedAndCancelledChecksCannotUseASnapshot()
    {
        var current = new CurrentUser { User = new UserIdentity { UserId = "alice", RoleIds = new[] { Guid.NewGuid() } } };
        var query = new PermissionQueryStub { FailNextRead = true };
        using var authorization = new UseCaseAuthorization(current, query);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => authorization.RequireAnyAsync(default, PermissionCodes.MenuGet));
        Assert.IsTrue((await authorization.RequireAnyAsync(default, PermissionCodes.MenuGet)).IsSuccess);
        Assert.AreEqual(2, query.Reads);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => authorization.RequireAnyAsync(cancellation.Token, PermissionCodes.MenuGet));
        Assert.AreEqual(2, query.Reads);
    }

    [TestMethod]
    public async Task SuccessfulPermissionWritesInvalidateTheSnapshotIncludingCombinedCommands()
    {
        foreach (var combined in new[] { false, true })
        {
            var roleId = Guid.NewGuid();
            var current = new CurrentUser { User = new UserIdentity { UserId = "alice", RoleIds = new[] { roleId } } };
            var query = new PermissionQueryStub();
            using var authorization = new UseCaseAuthorization(current, query);
            var transaction = new PermissionTransaction(query);
            Assert.IsTrue((await authorization.RequireAnyAsync(default, PermissionCodes.MenuGet)).IsSuccess);

            var result = combined
                ? await new RoleAccessAdministration(transaction, authorization, TimeProvider.System).ReplaceAsync(roleId, new(), new())
                : await new PermissionAdministration(new RolePermissions(), transaction, authorization).ReplaceAsync(roleId, new());

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue((await authorization.RequireAnyAsync(default, PermissionCodes.MenuGet)).IsFailed);
            Assert.AreEqual(2, query.Reads);
        }
    }

    private sealed class CurrentUser : ICurrentUser
    {
        public required UserIdentity User { get; set; }
    }

    private sealed class PermissionQueryStub : IPermissionQuery
    {
        public int Reads { get; private set; }
        public bool FailNextRead { get; set; }
        public List<string> Codes { get; set; } = new() { PermissionCodes.MenuGet, PermissionCodes.PermissionsWrite, PermissionCodes.MenuRoleMenus };

        public async Task<List<string>> GetPermissionCodesAsync(List<Guid> roleIds, string userId, CancellationToken cancellationToken = default)
        {
            Reads++;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if (FailNextRead)
            {
                FailNextRead = false;
                throw new InvalidOperationException("模拟权限存储临时故障。");
            }
            return new List<string>(Codes);
        }
    }

    private sealed class RolePermissions : IRolePermissionStore
    {
        public Task<List<string>> GetAsync(Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult(new List<string>());
    }

    private sealed class PermissionTransaction(PermissionQueryStub query) : IAccessControlTransaction, IAccessControlWriteSession
    {
        public Task<Result> Execute(Func<IAccessControlWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default) => operation(this);
        public Task<Result> ExecutePermissions(Func<IPermissionWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default) => operation(this);
        public Task ReplaceRolePermissions(Guid roleId, List<string> codes, CancellationToken cancellationToken = default)
        {
            query.Codes = new List<string>(codes);
            return Task.CompletedTask;
        }
        public Task<List<int>> LoadMenuIds(CancellationToken cancellationToken = default) => Task.FromResult(new List<int>());
        public Task<List<int>> LoadRoleMenus(Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult(new List<int>());
        public Task<bool> ApplyRoleChanges(Guid roleId, List<int> additions, List<int> removals, string userId, DateTime timestamp, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<Result> ExecuteMenus(Func<IMenuWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> ExecuteRoleMenus(Func<IRoleMenuWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<MenuState>> LoadMenus(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<MenuStructure>> LoadMenuStructure(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MenuState?> LoadMenu(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> Insert(MenuState menu, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> Update(MenuState menu, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> Remove(List<int> ids, string userId, DateTime timestamp, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
