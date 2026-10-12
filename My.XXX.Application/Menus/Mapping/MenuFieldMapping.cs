using My.XXX.Domain.Menus.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace My.XXX.Application.Menus.Mapping;

internal static class MenuFieldMapping
{
    private static readonly IReadOnlyDictionary<string, MenuOptionalField> Fields =
        new Dictionary<string, MenuOptionalField>(StringComparer.OrdinalIgnoreCase)
        {
            ["Description"] = MenuOptionalField.Description,
            ["Icon"] = MenuOptionalField.Icon,
            ["Url"] = MenuOptionalField.Address,
            ["Component"] = MenuOptionalField.View,
            ["ControllerName"] = MenuOptionalField.Resource,
            ["ActionName"] = MenuOptionalField.Operation,
            ["LinkTarget"] = MenuOptionalField.OpenMode
        };
    public static List<MenuOptionalField>? ToDomain(List<string>? fields) => fields?.Select(field =>
        field != null && Fields.TryGetValue(field, out var value) ? value : (MenuOptionalField)(-1)).ToList();
}
