using My.XXX.Domain.Abstractions;
using My.XXX.Domain.Authorization.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace My.XXX.Domain.Authorization.Policies;

public sealed record RoleMenuDelta(List<int> Additions, List<int> Removals);

public static class RoleMenuChanges
{
    public static DomainResult Validate(Guid roleId, IReadOnlyCollection<int>? ids, RoleMenuChange change) =>
        DomainResult.OkIf(roleId != Guid.Empty && ids != null && ids.All(id => id > 0) && Enum.IsDefined(change),
            RoleMenuErrors.InvalidSelection());

    public static DomainResult<RoleMenuDelta> Build(Guid roleId, IReadOnlyCollection<int>? ids, RoleMenuChange change,
        IReadOnlyCollection<int> available, IReadOnlyCollection<int> existing)
    {
        var validation = Validate(roleId, ids, change);
        if (validation.IsFailed) return DomainResult.Fail<RoleMenuDelta>(validation.Errors);
        var selected = ids!.Distinct().ToList();
        if (change != RoleMenuChange.Remove && selected.Except(available).Any())
            return DomainResult.Fail<RoleMenuDelta>(RoleMenuErrors.InvalidSelection());
        var removals = change == RoleMenuChange.Replace ? existing.Except(selected).ToList()
            : change == RoleMenuChange.Remove ? existing.Intersect(selected).ToList() : new();
        var additions = change == RoleMenuChange.Remove ? new List<int>() : selected.Except(existing).ToList();
        return DomainResult.Ok(new RoleMenuDelta(additions, removals));
    }
}
