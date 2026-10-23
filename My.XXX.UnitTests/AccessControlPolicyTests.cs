using My.XXX.Domain.Abstractions;
using My.XXX.Domain.Authorization.Models;
using My.XXX.Domain.Authorization.Policies;
using My.XXX.Domain.Menus.Models;
using My.XXX.Domain.Menus.Policies;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace My.XXX.UnitTests;

[TestClass]
public sealed class AccessControlPolicyTests
{
    [TestMethod]
    public void ParentCanOnlyBeDeletedWithItsChildren()
    {
        var menus = new List<MenuState> { new() { Id = 1, DisplayName = "root", CreatedBy = "test" }, new() { Id = 2, ParentId = 1, DisplayName = "child", CreatedBy = "test" } };
        var failure = MenuRemoval.Validate(menus, new[] { 1 });
        Assert.AreEqual("Menu.HasChildren", ((BusinessError)failure.Errors.Single()).Code);
        Assert.IsTrue(MenuRemoval.Validate(menus, new[] { 1, 2, 2 }).IsSuccess);
        Assert.IsTrue(MenuRemoval.Validate(menus, new[] { 3 }).IsFailed);
        Assert.IsTrue(MenuRemoval.Validate(menus, Array.Empty<int>()).IsFailed);
    }

    [TestMethod]
    public void ReplaceComputesOnlyChangedRelations()
    {
        var result = RoleMenuChanges.Build(Guid.NewGuid(), new[] { 2, 3, 3 }, RoleMenuChange.Replace,
            new[] { 1, 2, 3 }, new[] { 1, 2 });
        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(new[] { 3 }, result.Value.Additions);
        CollectionAssert.AreEqual(new[] { 1 }, result.Value.Removals);
    }

    [TestMethod]
    public void RemoveAllowsCleaningUpUnavailableMenusButAddRejectsThem()
    {
        var role = Guid.NewGuid();
        Assert.IsTrue(RoleMenuChanges.Build(role, new[] { 1 }, RoleMenuChange.Add,
            Array.Empty<int>(), Array.Empty<int>()).IsFailed);
        var removal = RoleMenuChanges.Build(role, new[] { 1 }, RoleMenuChange.Remove,
            Array.Empty<int>(), new[] { 1 });
        CollectionAssert.AreEqual(new[] { 1 }, removal.Value.Removals);
        Assert.IsTrue(RoleMenuChanges.Validate(role, new[] { 1 }, (RoleMenuChange)99).IsFailed);
    }

    [TestMethod]
    public void PermissionsMustBelongToTheStableCatalog()
    {
        var role = Guid.NewGuid();
        Assert.IsTrue(PermissionSelection.Validate(role, new[] { PermissionCodes.MenuAdd }).IsSuccess);
        Assert.IsTrue(PermissionSelection.Validate(role, Array.Empty<string>()).IsSuccess);
        Assert.IsTrue(PermissionSelection.Validate(role, new[] { "unknown" }).IsFailed);
        Assert.IsTrue(PermissionSelection.Validate(Guid.Empty, Array.Empty<string>()).IsFailed);
    }
}
