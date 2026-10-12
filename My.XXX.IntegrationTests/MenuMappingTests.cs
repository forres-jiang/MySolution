using My.XXX.Persistence.Mapping;
using My.XXX.Persistence.PersistenceModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace My.XXX.IntegrationTests;

[TestClass]
public sealed class MenuMappingTests
{
    [TestMethod]
    public void NavigationAndAuditFieldsSurvivePersistenceRoundTrip()
    {
        var row = new Menus
        {
            Id = 3,
            DisplayName = "menu",
            Url = "/route",
            Component = "view",
            ControllerName = "resource",
            ActionName = "operation",
            LinkTarget = "_blank",
            ParentId = 2,
            Number = 7,
            IsDisplay = true,
            CreatedBy = "alice",
            UpdatedBy = "bob",
            CreatedAt = new DateTime(2026, 1, 1),
            UpdatedAt = new DateTime(2026, 1, 2)
        };
        var mapper = new PersistenceMapper();
        var state = mapper.ToMenuState(row)!;
        var saved = mapper.ToMenuEntity(state)!;
        Assert.AreEqual(row.Url, saved.Url);
        Assert.AreEqual(row.Component, saved.Component);
        Assert.AreEqual(row.ControllerName, saved.ControllerName);
        Assert.AreEqual(row.ActionName, saved.ActionName);
        Assert.AreEqual(row.LinkTarget, saved.LinkTarget);
        Assert.AreEqual(row.CreatedAt, saved.CreatedAt);
        Assert.AreEqual(row.UpdatedAt, saved.UpdatedAt);
        Assert.AreEqual(DateTimeKind.Utc, state.CreatedAt.Kind);
        Assert.AreEqual(row.ParentId, saved.ParentId);
        Assert.AreEqual(row.Number, saved.Number);
        Assert.AreEqual(row.UpdatedBy, saved.UpdatedBy);
    }
}
