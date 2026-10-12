namespace My.XXX.Application.Menus.Models;

/// <summary>规范化查询的排序意图，由持久层在数据库端执行，保证跨页顺序一致。</summary>
public enum MenuSort
{
    /// <summary>按创建时间倒序（默认列表视图）。</summary>
    RecentFirst,
    /// <summary>按父级、序号升序（选择器视图）。</summary>
    ParentThenNumber
}

/// <summary>规范化的查询条件。PageIndex 从零开始，与 HTTP 绑定无关。</summary>
public sealed record MenuSearch(string DisplayName, bool? IsAction, bool IsDisplay, int? ParentId, int PageIndex, int PageSize, MenuSort Sort = MenuSort.RecentFirst);
