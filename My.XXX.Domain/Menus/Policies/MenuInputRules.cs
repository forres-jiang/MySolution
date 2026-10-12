using My.XXX.Domain.Abstractions;
using My.XXX.Domain.Menus.Models;
using System.Collections.Generic;

namespace My.XXX.Domain.Menus.Policies;

public static class MenuInputRules
{
    public static DomainResult Create(string displayName, bool? isAction, int parentId, int number) =>
        DomainResult.OkIf(!string.IsNullOrWhiteSpace(displayName) && isAction.HasValue && parentId >= 0 && number >= 0,
            MenuErrors.InvalidInput());

    public static DomainResult Modify(int id, int? parentId, int? number, List<MenuOptionalField>? clearFields) =>
        DomainResult.OkIf(id > 0 && !(parentId < 0) && !(number < 0) && MenuUpdateFields.Valid(clearFields),
            MenuErrors.InvalidInput());
}
