using System.Collections.Generic;

namespace My.XXX.APIs.Contracts.Shared;

/// <summary>分页响应的 wire 契约包装；字段形状与历史 Paged 响应保持一致（total/list）。</summary>
public class PagedResponse<T>
{
    public int Total { get; set; }

    public List<T> List { get; set; }

    public static PagedResponse<T> Create(List<T> data, int total) => new() { Total = total, List = data };
}
