using My.XXX.Domain.Menus.Models;
using System.Collections.Generic;
using System.Linq;
namespace My.XXX.Domain.Menus.Policies;

public static class MenuHierarchy
{
    public static bool CanPlace(IReadOnlyCollection<IMenuStructure> menus, int id, int parentId, bool isAction)
    {
        if (isAction && menus.Any(m => m.ParentId == id && id != 0)) return false;
        if (parentId == 0) return !isAction;
        var byId = menus.ToDictionary(m => m.Id);
        var visited = new HashSet<int>();
        while (parentId != 0)
        {
            if (parentId == id || !visited.Add(parentId) || !byId.TryGetValue(parentId, out var parent) || parent.IsAction) return false;
            parentId = parent.ParentId;
        }
        return true;
    }
}
