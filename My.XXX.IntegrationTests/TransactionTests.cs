using My.XXX.Application.AccessControl.Ports;
using My.XXX.Domain.Authentication.Models;
using My.XXX.Domain.Menus.Models;
using My.XXX.Persistence;
using My.XXX.Persistence.Migrations;
using My.XXX.Persistence.PersistenceModels;
using My.XXX.Persistence.Repositories;
using FluentResults;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

[assembly: DoNotParallelize]

namespace My.XXX.IntegrationTests;

[TestClass]
public sealed class TransactionTests
{
    private static PostgresFixture database;

    [ClassInitialize]
    public static async Task Start(TestContext context) => database = await PostgresFixture.Start();

    [ClassCleanup]
    public static async Task Stop() { if (database != null) await database.DisposeAsync(); }

    [TestMethod]
    public async Task MenuWritesDoNotInvalidatePermissions()
    {
        using var db = database.Open();
        var before = await Revision(db);
        var result = await new AccessControlTransaction(db).ExecuteMenus(async session =>
            Result.OkIf(await session.Insert(Menu(Guid.NewGuid().ToString())) == 1, "insert failed"));
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(before, await Revision(db));
    }

    [TestMethod]
    public async Task CombinedFailureRollsBackAllWritesAndRevision()
    {
        using var db = database.Open();
        var before = await Revision(db);
        var role = Guid.NewGuid();
        var name = Guid.NewGuid().ToString();
        var result = await new AccessControlTransaction(db).Execute(async session =>
        {
            await session.Insert(Menu(name));
            await session.ReplaceRolePermissions(role, new() { "menu.get" });
            return Result.Fail("second operation rejected");
        });
        Assert.IsTrue(result.IsFailed);
        Assert.AreEqual(0, await db.GetTable<Menus>().CountAsync(m => m.DisplayName == name));
        Assert.AreEqual(0, await db.GetTable<RolePermission>().CountAsync(p => p.RoleId == role));
        Assert.AreEqual(before, await Revision(db));
    }

    [TestMethod]
    public async Task ExceptionRollsBackAndTransactionCanBeReused()
    {
        using var db = database.Open();
        var transaction = new AccessControlTransaction(db);
        var role = Guid.NewGuid();
        var before = await Revision(db);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => transaction.ExecutePermissions(async session =>
        {
            await session.ReplaceRolePermissions(role, new() { "menu.get" });
            throw new InvalidOperationException("injected failure");
        }));
        Assert.AreEqual(before, await Revision(db));
        Assert.AreEqual(0, await db.GetTable<RolePermission>().CountAsync(p => p.RoleId == role));
        Assert.IsTrue((await transaction.ExecutePermissions(async session =>
        {
            await session.ReplaceRolePermissions(role, new() { "menu.edit" });
            return Result.Ok();
        })).IsSuccess);
    }

    [TestMethod]
    public async Task CancellationRollsBackEvenWhenCleanupTokenIsCancelled()
    {
        using var db = database.Open();
        using var cancelled = new CancellationTokenSource();
        var role = Guid.NewGuid();
        var before = await Revision(db);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => new AccessControlTransaction(db).ExecutePermissions(async session =>
        {
            await session.ReplaceRolePermissions(role, new() { "menu.get" });
            cancelled.Cancel();
            return Result.Ok();
        }, cancelled.Token));
        Assert.AreEqual(before, await Revision(db));
        Assert.AreEqual(0, await db.GetTable<RolePermission>().CountAsync(p => p.RoleId == role));
    }

    [TestMethod]
    public async Task PermissionWritesCanCompleteWhileMenuWriteIsOpen()
    {
        using var menuDb = database.Open();
        using var permissionDb = database.Open();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var menuTask = new AccessControlTransaction(menuDb).ExecuteMenus(async session =>
        {
            entered.SetResult();
            await release.Task;
            return Result.Ok();
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var result = await new AccessControlTransaction(permissionDb).ExecutePermissions(async session =>
            {
                await session.ReplaceRolePermissions(Guid.NewGuid(), new() { "menu.get" });
                return Result.Ok();
            }).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(result.IsSuccess);
        }
        finally { release.TrySetResult(); await menuTask; }
    }

    [TestMethod]
    public async Task NarrowSessionsRejectOtherCapabilitiesAndExpiredUsage()
    {
        using var db = database.Open();
        IMenuWriteSession? captured = null;
        await new AccessControlTransaction(db).ExecuteMenus(async session =>
        {
            captured = session;
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                ((IPermissionWriteSession)session).ReplaceRolePermissions(Guid.NewGuid(), new()));
            return Result.Ok();
        });
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => captured!.LoadMenus());
    }

    [TestMethod]
    public async Task MigrationsAreRepeatableAndVerified()
    {
        await MigrationRunner.ApplyAsync(database.ConnectionString, DatabaseProvider.PostgreSQL);
        await MigrationRunner.VerifyAsync(database.ConnectionString, DatabaseProvider.PostgreSQL);
        using var db = database.Open();
        Assert.AreEqual(1, await db.GetTable<MenuWriteLock>().CountAsync());
    }

    [TestMethod]
    public async Task AuthenticationSupportsUtcRotationAndRevocation()
    {
        using var db = database.Open();
        var store = new AuthenticationStore(db);
        var userId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var sessionId = Guid.NewGuid().ToString("N");
        var tokenId = Guid.NewGuid().ToString("N");
        var nextTokenId = Guid.NewGuid().ToString("N");
        await store.SetUserAsync(new UserIdentity
        {
            UserId = userId,
            UserName = "测试用户",
            Email = "test@example.com",
            RoleIds = new[] { Guid.NewGuid() }
        }, true);
        await store.CreateSessionAsync(new SessionState
        {
            SessionId = sessionId,
            UserId = userId,
            RefreshTokenId = tokenId,
            ExpiresUtc = now.AddHours(1)
        });
        var active = await store.GetActiveSessionAsync(sessionId, now);
        Assert.IsNotNull(active);
        Assert.AreEqual("测试用户", active.User.UserName);
        Assert.AreEqual(DateTimeKind.Utc, active.Session.ExpiresUtc.Kind);
        Assert.IsTrue(await store.RotateAsync(sessionId, tokenId, nextTokenId, now));
        Assert.IsFalse(await store.RotateAsync(sessionId, tokenId, Guid.NewGuid().ToString("N"), now));
        Assert.IsNull(await store.GetActiveSessionAsync(sessionId, now.AddHours(2)));
        await store.RevokeAsync(sessionId);
        Assert.IsNull(await store.GetActiveSessionAsync(sessionId, now));
    }

    private static MenuState Menu(string name) => new()
    {
        DisplayName = name,
        CreatedBy = "test",
        UpdatedBy = "test",
        CreatedAt = DateTime.Now,
        UpdatedAt = DateTime.Now
    };
    private static Task<long> Revision(DBContext db) =>
        db.GetTable<PermissionRevision>().Where(r => r.Id == 1).Select(r => r.Version).SingleAsync();
}
