using My.XXX.Domain.Abstractions;
using My.XXX.Domain.Menus.Models;
using System.Collections.Generic;
using System.Linq;

namespace My.XXX.Domain.Menus.Policies;

public static class MenuRemoval
{
    public static DomainResult ValidateSelection(IReadOnlyCollection<int> ids) =>
        DomainResult.OkIf(ids != null && ids.Count > 0 && ids.All(id => id > 0), MenuErrors.InvalidInput());

    public static DomainResult Validate(IReadOnlyCollection<IMenuStructure> menus, IReadOnlyCollection<int> ids)
    {
        var validation = ValidateSelection(ids);
        if (validation.IsFailed) return validation;
        var selected = ids.ToHashSet();
        if (selected.Except(menus.Select(m => m.Id)).Any()) return DomainResult.Fail(MenuErrors.NotFound());
        if (menus.Any(m => selected.Contains(m.ParentId) && !selected.Contains(m.Id)))
            return DomainResult.Fail(MenuErrors.HasChildren());
        return DomainResult.Ok();
    }
}
