using My.XXX.Domain.Menus.Models;
using My.XXX.Domain.Menus.Policies;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace My.XXX.UnitTests;

[TestClass]
public sealed class MenuPolicyTests
{
    [TestMethod]
    public void InputsRejectInvalidFieldsAndAllowPartialUpdates()
    {
        Assert.IsTrue(MenuInputRules.Create("menu", false, 0, 0).IsSuccess);
        Assert.IsTrue(MenuInputRules.Create(" ", false, 0, 0).IsFailed);
        Assert.IsTrue(MenuInputRules.Create("menu", null, 0, 0).IsFailed);
        Assert.IsTrue(MenuInputRules.Modify(1, null, null, null).IsSuccess);
        Assert.IsTrue(MenuInputRules.Modify(1, -1, null, null).IsFailed);
        Assert.IsTrue(MenuInputRules.Modify(1, null, null, new() { (MenuOptionalField)99 }).IsFailed);
        Assert.IsTrue(MenuInputRules.Modify(1, null, null, new() { MenuOptionalField.Description }).IsSuccess);
    }

    [TestMethod]
    public void MovingParentBelowDescendantIsRejected()
    {
        var menus = new List<MenuState> { new() { Id = 1, DisplayName = "root", CreatedBy = "test" }, new() { Id = 2, ParentId = 1, DisplayName = "child", CreatedBy = "test" } };
        Assert.IsFalse(MenuHierarchy.CanPlace(menus, 1, 2, false));
        Assert.IsTrue(MenuHierarchy.CanPlace(menus, 2, 0, false));
    }

    [TestMethod]
    public void ActionCannotBeRootOrParent()
    {
        var menus = new List<MenuState> { new() { Id = 1, IsAction = true, DisplayName = "action", CreatedBy = "test" } };
        Assert.IsFalse(MenuHierarchy.CanPlace(menus, 2, 0, true));
        Assert.IsFalse(MenuHierarchy.CanPlace(menus, 2, 1, false));
    }
}
