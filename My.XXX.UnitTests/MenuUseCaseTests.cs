using My.XXX.Application.Abstractions;
using My.XXX.Application.Abstractions.Ports;
using My.XXX.Application.Abstractions.Results;
using My.XXX.Application.AccessControl.Ports;
using My.XXX.Application.Menus;
using My.XXX.Application.Menus.Mapping;
using My.XXX.Application.Menus.Models;
using My.XXX.Application.Menus.Ports;
using My.XXX.Domain.Authentication.Models;
using My.XXX.Domain.Menus.Models;
using FluentResults;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.UnitTests;

[TestClass]
public sealed class MenuUseCaseTests
{
    [TestMethod]
    public async Task PublicCommandEntryRejectsInvalidHierarchyInsideTransaction()
    {
        var storage = new Menus();
        var service = new MenuCommandService(storage, AuthorizationTestSupport.Admin(), TimeProvider.System);
        var result = await service.Update(new MenuModification { Id = 1, ParentId = 2 });
        Assert.IsTrue(storage.Entered);
        Assert.AreEqual("Menu.InvalidParent", ((BusinessFailure)result.Errors.Single()).Code);
        Assert.AreEqual(0, storage.Writes);
        Assert.AreEqual(0, storage.Rows[0].ParentId);
        result = await service.Remove(new() { 1 });
        Assert.AreEqual("Menu.HasChildren", ((BusinessFailure)result.Errors.Single()).Code);
        Assert.AreEqual(0, storage.Writes);
    }

    [TestMethod]
    public async Task PublicCommandEntryValidatesAndStampsAllowedChanges()
    {
        var storage = new Menus();
        var clock = new FixedClock();
        var service = new MenuCommandService(storage, AuthorizationTestSupport.Admin(), clock);
        var result = await service.Update(new MenuModification { Id = 2, DisplayName = " updated " });
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, storage.Writes);
        Assert.IsNotNull(storage.Written);
        Assert.AreEqual("updated", storage.Written.DisplayName);
        Assert.AreEqual("alice", storage.Written.UpdatedBy);
        Assert.AreEqual(clock.GetUtcNow().UtcDateTime, storage.Written.UpdatedAt);
        Assert.AreEqual(0, storage.FullReads);
    }

    [TestMethod]
    public async Task QueryBuildsCheckedTreeWithoutChangingStoredState()
    {
        var storage = new Menus();
        var query = new MenuQueryService(storage, new ApplicationMapper(), AuthorizationTestSupport.Admin());
        var result = await query.GetMenuTreeCheckedByRoles(new() { Guid.NewGuid() });
        Assert.IsTrue(result.IsSuccess);
        var tree = result.Value;
        Assert.AreEqual(1, tree.Count);
        Assert.IsFalse(tree[0].Checked);
        Assert.IsTrue(tree[0].Children!.Single().Checked);
        Assert.AreEqual(2, tree[0].Children!.Single().Id);
        tree[0].DisplayName = "client edit";
        Assert.AreEqual("root", storage.Rows[0].DisplayName);
    }

    [TestMethod]
    public async Task LegacyNavigationFieldsAreMappedAndClearedWithoutMutatingSnapshot()
    {
        var storage = new Menus();
        storage.Rows[1] = storage.Rows[1] with { Destination = new MenuDestination("/old", "old-view", "old-resource", "old-operation", "_blank") };
        var service = new MenuCommandService(storage, AuthorizationTestSupport.Admin(), new FixedClock());
        var result = await service.Update(new MenuModification { Id = 2, ControllerName = " new-resource ", ClearFields = new() { "url", "Component" } });
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(storage.Written);
        Assert.IsNull(storage.Written.Destination.Address);
        Assert.IsNull(storage.Written.Destination.View);
        Assert.AreEqual("new-resource", storage.Written.Destination.Resource);
        Assert.AreEqual("old-operation", storage.Written.Destination.Operation);
        Assert.AreEqual("/old", storage.Rows[1].Destination.Address);
        var view = new ApplicationMapper().ToMenuView(storage.Written)!;
        Assert.IsNull(view.Url);
        Assert.AreEqual("new-resource", view.ControllerName);
        Assert.AreEqual("_blank", view.LinkTarget);
    }

    [TestMethod]
    public async Task ReorderingOnlyWritesChangedPositionsAndPreservesUnchangedAudit()
    {
        var storage = new Menus();
        var original = storage.Rows[1] with { Number = 0 };
        storage.Rows[1] = original;
        storage.Rows.Add(original with { Id = 3, Number = 1 });
        storage.Rows.Add(original with { Id = 4, Number = 2 });
        var clock = new FixedClock();
        var service = new MenuCommandService(storage, AuthorizationTestSupport.Admin(), clock);

        Assert.IsTrue((await service.UpdateSort(new MenuReorderCommand(2, 0, 3))).IsSuccess);
        Assert.AreEqual(0, storage.Writes);
        Assert.IsTrue((await service.UpdateSort(new MenuReorderCommand(2, 3, 4))).IsSuccess);
        CollectionAssert.AreEquivalent(new[] { 2, 3 }, storage.Updates.Select(menu => menu.Id).ToArray());
        Assert.IsTrue(storage.Updates.All(menu => menu.UpdatedAt == clock.GetUtcNow().UtcDateTime));
        Assert.IsNull(storage.Rows.Single(menu => menu.Id == 4).UpdatedAt);
    }

    private sealed class CurrentUser : ICurrentUser
    {
        public UserIdentity User { get; } = new() { UserId = "alice" };
    }
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("test-east", TimeSpan.FromHours(8), "test-east", "test-east");
    }
    private sealed class Menus : IAccessControlTransaction, IMenuWriteSession, IMenuReadRepository
    {
        public List<MenuState> Rows = new() { new() { Id = 1, CreatedBy = "test", DisplayName = "root" }, new() { Id = 2, ParentId = 1, DisplayName = "child", CreatedBy = "test" } };
        public bool Entered;
        public int Writes;
        public MenuState? Written;
        public List<MenuState> Updates { get; } = new();
        public async Task<Result> ExecuteMenus(Func<IMenuWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default)
        {
            Entered = true;
            return await operation(this);
        }
        public Task<Result> Execute(Func<IAccessControlWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> ExecuteRoleMenus(Func<IRoleMenuWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> ExecutePermissions(Func<IPermissionWriteSession, Task<Result>> operation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public int FullReads;
        public Task<List<MenuState>> LoadMenus(CancellationToken cancellationToken = default) { FullReads++; return Task.FromResult(Rows); }
        public Task<List<MenuStructure>> LoadMenuStructure(CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.Select(m => new MenuStructure(m.Id, m.ParentId, m.IsAction)).ToList());
        public Task<MenuState?> LoadMenu(int id, CancellationToken cancellationToken = default) => Get(id, cancellationToken);
        public Task<int> Insert(MenuState menu, CancellationToken cancellationToken = default) { Writes++; Written = menu; return Task.FromResult(1); }
        public Task<int> Update(MenuState menu, CancellationToken cancellationToken = default)
        {
            Writes++;
            Written = menu;
            Updates.Add(menu);
            return Task.FromResult(1);
        }
        public Task<int> Remove(List<int> ids, string userId, DateTime timestamp, CancellationToken cancellationToken = default) { Writes++; return Task.FromResult(ids.Count); }
        public Task<MenuState?> Get(int id, CancellationToken cancellationToken = default) => Task.FromResult(Rows.SingleOrDefault(row => row.Id == id));
        public Task<Paged<MenuState>> Search(MenuSearch query, CancellationToken cancellationToken = default) => Task.FromResult(Paged<MenuState>.Create(Rows, Rows.Count));
        public Task<List<MenuState>> GetMenus(int? parentId = null, List<int>? ids = null, bool? isDisplay = null, CancellationToken cancellationToken = default) => Task.FromResult(Rows);
        public Task<List<MenuState>> GetRoleMenuByRoles(List<Guid> roleIds, CancellationToken cancellationToken = default) => Task.FromResult(new List<MenuState> { Rows[1] });
    }
}
