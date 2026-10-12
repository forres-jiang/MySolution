using My.XXX.Application.Menus.Models;
using System.Collections.Generic;
using System.Linq;
namespace My.XXX.Application.Menus.Queries;

public static class MenuTree
{
    public static List<MenuNode> Build(List<MenuNode> menus)
    {
        var children = menus.ToLookup(m => m.Menu.ParentId);
        var visited = new HashSet<int>();
        List<MenuNode> Visit(int parentId)
        {
            var result = new List<MenuNode>();
            foreach (var menu in children[parentId].OrderBy(m => m.Menu.Number).ThenBy(m => m.Menu.UpdatedAt))
            {
                if (!visited.Add(menu.Menu.Id)) continue;
                menu.Children = Visit(menu.Menu.Id);
                result.Add(menu);
            }
            return result;
        }
        return Visit(0);
    }
}
