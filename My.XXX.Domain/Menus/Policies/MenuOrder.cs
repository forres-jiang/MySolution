using My.XXX.Domain.Menus.Models;
using System.Collections.Generic;
using System.Linq;
namespace My.XXX.Domain.Menus.Policies;

/// <summary>纯排序策略。仓储在其写锁保护下提供快照。</summary>
public static class MenuOrder
{
    public static List<MenuState>? Build(List<MenuState> menus, MenuReorderCommand command)
    {
        if (command == null || command.CurrentId <= 0 || command.PrevId == command.NextId ||
            command.CurrentId == command.PrevId || command.CurrentId == command.NextId) return null;
        var byId = menus.Select(m => m.Copy()).ToDictionary(m => m.Id);
        if (!byId.TryGetValue(command.CurrentId, out var current)) return null;
        var anchorId = command.PrevId == 0 ? command.NextId : command.PrevId;
        if (!byId.TryGetValue(anchorId, out var anchor)) return null;
        // 将节点移动到自身或其某个后代之下会破坏树结构。
        var parent = anchor.ParentId;
        var visited = new HashSet<int>();
        while (parent != 0)
        {
            if (parent == current.Id || !visited.Add(parent) || !byId.TryGetValue(parent, out var node)) return null;
            parent = node.ParentId;
        }
        if (command.PrevId != 0 && command.NextId != 0 &&
            (!byId.TryGetValue(command.NextId, out var next) || next.ParentId != anchor.ParentId)) return null;
        var siblings = byId.Values.Where(m => m.ParentId == anchor.ParentId && m.Id != current.Id)
            .OrderBy(m => m.Number).ThenBy(m => m.UpdatedAt).ToList();
        var index = siblings.FindIndex(m => m.Id == anchorId);
        if (index < 0) return null;
        current = current with { ParentId = anchor.ParentId };
        siblings.Insert(command.PrevId == 0 ? index : index + 1, current);
        return siblings;
    }
}
