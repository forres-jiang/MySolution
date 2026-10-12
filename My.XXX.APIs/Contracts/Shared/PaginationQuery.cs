using System.ComponentModel.DataAnnotations;

namespace My.XXX.APIs.Contracts.Shared;

/// <summary>HTTP 分页输入，由应用层边界统一规范化页码和大小。</summary>
public class PaginationQuery
{
    [Required]
    [Range(1, int.MaxValue)]
    public int PageSize { get; set; }

    [Required]
    public int PageIndex { get; set; }
}
