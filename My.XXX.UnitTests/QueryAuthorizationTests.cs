using My.XXX.Application.Abstractions.Results;
using My.XXX.Application.Authorization;
using My.XXX.Application.Menus;
using My.XXX.Application.Menus.Mapping;
using My.XXX.Application.Menus.Models;
using My.XXX.Domain.Authentication.Models;
using My.XXX.Domain.Authorization.Policies;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace My.XXX.UnitTests;

[TestClass]
public sealed class QueryAuthorizationTests
{
    [TestMethod]
    public async Task AllProtectedQueriesRejectUnauthorizedCallersBeforeStorage()
    {
        foreach (var user in new UserIdentity?[] { null, new() { UserId = "reader" } })
        {
            var authorization = AuthorizationTestSupport.Create(user);
            var menus = new MenuQueryService(null!, new ApplicationMapper(), authorization);
            var permissions = new PermissionAdministration(null!, null!, authorization);
            var query = new MenuListingQuery("", null, false, null, 1, 10);
            var results = new FluentResults.ResultBase[]
            {
                await menus.FindAsync(1), await menus.GetMenus(query), await menus.SearchMenus(query),
                await menus.GetTreeMenus(null), await menus.GetTreeMenus(true),
                await menus.GetMenuTreeCheckedByRole(Guid.NewGuid()),
                await menus.GetMenuTreeCheckedByRoles(new() { Guid.NewGuid() }),
                await permissions.GetAsync(Guid.NewGuid())
            };
            foreach (var result in results)
                Assert.AreEqual(user == null ? "Authentication.Required" : "Authorization.Forbidden",
                    ((BusinessFailure)result.Errors.Single()).Code);
        }
    }

    [TestMethod]
    public async Task PermissionsForDistinctQueriesCannotBeSubstituted()
    {
        var user = new UserIdentity { UserId = "reader", RoleIds = new[] { Guid.NewGuid() } };
        var menus = new MenuQueryService(null!, new ApplicationMapper(),
            AuthorizationTestSupport.Create(user, PermissionCodes.MenuTreeByRoleId, PermissionCodes.MenuDisplayTree, PermissionCodes.MenuSearch));
        Assert.IsTrue((await menus.GetMenuTreeCheckedByRoles(new() { Guid.NewGuid() })).IsFailed);
        Assert.IsTrue((await menus.GetTreeMenus(null)).IsFailed);
        Assert.IsTrue((await menus.GetMenus(new("", null, false, null, 1, 10))).IsFailed);
    }
}
