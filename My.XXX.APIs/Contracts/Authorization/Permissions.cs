using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace My.XXX.APIs.Contracts.Authorization;

/// <summary>替换某角色的权限码集合。用 DTO 而非裸 List&lt;string&gt; 承载请求体，保持契约可演进。</summary>
public class ReplacePermissionsRequest
{
    [Required]
    public List<string> Codes { get; set; } = new();
}
