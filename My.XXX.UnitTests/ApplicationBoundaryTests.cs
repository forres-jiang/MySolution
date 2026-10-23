using My.XXX.Application.Abstractions.Ports;
using My.XXX.Application.AccessControl.Ports;
using My.XXX.Application.Authorization;
using My.XXX.Application.Menus;
using My.XXX.Domain.Authentication.Models;
using My.XXX.Domain.Authorization.Models;
using My.XXX.Domain.Authorization.Policies;
using FluentResults;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.UnitTests;

[TestClass]
public sealed class ApplicationBoundaryTests
{
    [TestMethod]
    public async Task InvalidRequestsDoNotOpenTransactions()
    {
        var transaction = new Transaction();
        var user = new CurrentUserStub();
        var menu = new MenuCommandService(transaction, AuthorizationTestSupport.Admin(), TimeProvider.System);
        Assert.IsTrue((await menu.Add(null!)).IsFailed);
        Assert.IsTrue((await menu.Update(new() { Id = 1, ClearFields = new() { "Id" } })).IsFailed);
        var permissions = new PermissionAdministration(null!, transaction, AuthorizationTestSupport.Admin());
        Assert.IsTrue((await permissions.ReplaceAsync(Guid.NewGuid(), new() { "unknown" })).IsFailed);
        var combined = new RoleAccessAdministration(transaction, AuthorizationTestSupport.Admin(), TimeProvider.System);
        Assert.IsTrue((await combined.ReplaceAsync(Guid.NewGuid(), new() { 1 }, new() { "unknown" })).IsFailed);
        Assert.IsNull(transaction.Scope);
    }

    [TestMethod]
    public async Task UseCasesSelectOnlyTheirRequiredWriteCapabilities()
    {
        var transaction = new Transaction();
        var user = new CurrentUserStub();
        var menu = new MenuCommandService(transaction, AuthorizationTestSupport.Admin(), TimeProvider.System);
        await menu.Remove(new() { 1 });
        Assert.AreEqual("menus", transaction.Scope);
        var roles = new RoleMenuAssignmentService(transaction, AuthorizationTestSupport.Admin(), TimeProvider.System);
        await roles.SetRoleMenus(Guid.NewGuid(), new() { 1 }, RoleMenuChange.Add);
        Assert.AreEqual("role-menus", transaction.Scope);
        var permissions = new PermissionAdministration(null!, transaction, AuthorizationTestSupport.Admin());
        await permissions.ReplaceAsync(Guid.NewGuid(), new() { PermissionCodes.MenuGet });
        Assert.AreEqual("permissions", transaction.Scope);
        var combined = new RoleAccessAdministration(transaction, AuthorizationTestSupport.Admin(), TimeProvider.System);
        await combined.ReplaceAsync(Guid.NewGuid(), new() { 1 }, new() { PermissionCodes.MenuGet });
        Assert.AreEqual("combined", transaction.Scope);
    }

    private sealed class CurrentUserStub : ICurrentUser { public UserIdentity? User => null; }

    private sealed class Transaction : IAccessControlTransaction
    {
        public string? Scope;
        private Task<Result> Select(string scope) { Scope = scope; return Task.FromResult(Result.Ok()); }
        public Task<Result> Execute(Func<IAccessControlWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default) => Select("combined");
        public Task<Result> ExecuteMenus(Func<IMenuWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default) => Select("menus");
        public Task<Result> ExecuteRoleMenus(Func<IRoleMenuWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default) => Select("role-menus");
        public Task<Result> ExecutePermissions(Func<IPermissionWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default) => Select("permissions");
    }
}
