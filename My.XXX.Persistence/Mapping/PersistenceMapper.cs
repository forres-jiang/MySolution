using My.XXX.Domain.Menus.Models;
using My.XXX.Persistence.PersistenceModels;
using System.Collections.Generic;
using System.Linq;

namespace My.XXX.Persistence.Mapping;

public sealed class PersistenceMapper
{
    public MenuState? ToMenuState(Menus? source) => source == null ? null : new MenuState
    {
        Id = source.Id,
        DisplayName = source.DisplayName ?? string.Empty,
        Description = source.Description,
        Icon = source.Icon,
        Number = source.Number,
        ParentId = source.ParentId,
        IsDisplay = source.IsDisplay,
        IsAction = source.IsAction,
        IsDeleted = source.IsDeleted,
        CreatedBy = source.CreatedBy,
        CreatedAt = System.DateTime.SpecifyKind(source.CreatedAt, System.DateTimeKind.Utc),
        UpdatedBy = source.UpdatedBy ?? string.Empty,
        UpdatedAt = source.UpdatedAt is { } updated ? System.DateTime.SpecifyKind(updated, System.DateTimeKind.Utc) : null,
        Destination = new MenuDestination(source.Url, source.Component, source.ControllerName, source.ActionName, source.LinkTarget)
    };
    public List<MenuState> ToMenuStates(IEnumerable<Menus>? source) => source?.Select(m => ToMenuState(m)!).ToList() ?? new();
    public Menus? ToMenuEntity(MenuState? source) => source == null ? null : new Menus
    {
        Id = source.Id,
        DisplayName = source.DisplayName ?? string.Empty,
        Description = source.Description,
        Icon = source.Icon,
        Number = source.Number,
        ParentId = source.ParentId,
        IsDisplay = source.IsDisplay,
        IsAction = source.IsAction,
        IsDeleted = source.IsDeleted,
        CreatedBy = source.CreatedBy,
        CreatedAt = System.DateTime.SpecifyKind(source.CreatedAt, System.DateTimeKind.Utc),
        UpdatedBy = source.UpdatedBy ?? string.Empty,
        UpdatedAt = source.UpdatedAt is { } updated ? System.DateTime.SpecifyKind(updated, System.DateTimeKind.Utc) : null,
        Url = source.Destination.Address,
        Component = source.Destination.View,
        ControllerName = source.Destination.Resource,
        ActionName = source.Destination.Operation,
        LinkTarget = source.Destination.OpenMode,
    };
}
