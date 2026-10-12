using My.XXX.Domain.Abstractions;
using My.XXX.Domain.Menus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
namespace My.XXX.Domain.Menus.Policies;

/// <summary>Returns a validated copy; rejected transitions never mutate the loaded snapshot.</summary>
public static class MenuTransitions
{
    public static DomainResult<MenuState> Update(List<MenuState> menus, MenuUpdate input, string userId, DateTime timestamp)
        => Update(menus.FirstOrDefault(m => m.Id == input?.Id), menus, input, userId, timestamp);

    public static DomainResult<MenuState> Update(MenuState? current, IReadOnlyCollection<IMenuStructure> menus,
        MenuUpdate input, string userId, DateTime timestamp)
    {
        if (input == null) return DomainResult.Fail<MenuState>(MenuErrors.InvalidInput());
        var validation = MenuInputRules.Modify(input.Id, input.ParentId, input.Number, input.ClearFields);
        if (validation.IsFailed) return DomainResult.Fail<MenuState>(validation.Errors);
        var menu = current?.Copy();
        if (menu == null || menu.Id != input.Id) return DomainResult.Fail<MenuState>(MenuErrors.NotFound());
        menu = menu with { ParentId = input.ParentId ?? menu.ParentId, IsAction = input.IsAction ?? menu.IsAction };
        if (!MenuHierarchy.CanPlace(menus, menu.Id, menu.ParentId, menu.IsAction)) return DomainResult.Fail<MenuState>(MenuErrors.InvalidParent());
        var clear = new HashSet<MenuOptionalField>(input.ClearFields ?? new());
        string? Value(MenuOptionalField field, string? value, string? old) => clear.Contains(field) ? null : value?.Trim() ?? old;
        menu = menu with
        {
            Description = Value(MenuOptionalField.Description, input.Description, menu.Description),
            Icon = Value(MenuOptionalField.Icon, input.Icon, menu.Icon),
            Destination = new MenuDestination(
                Value(MenuOptionalField.Address, input.Address, menu.Destination.Address),
                Value(MenuOptionalField.View, input.View, menu.Destination.View),
                Value(MenuOptionalField.Resource, input.Resource, menu.Destination.Resource),
                Value(MenuOptionalField.Operation, input.Operation, menu.Destination.Operation),
                Value(MenuOptionalField.OpenMode, input.OpenMode, menu.Destination.OpenMode)),
            DisplayName = string.IsNullOrWhiteSpace(input.DisplayName) ? menu.DisplayName : input.DisplayName.Trim(),
            Number = input.Number ?? menu.Number,
            IsDisplay = input.IsDisplay ?? menu.IsDisplay,
            UpdatedBy = userId,
            UpdatedAt = timestamp
        };
        return DomainResult.Ok(menu);
    }
}
