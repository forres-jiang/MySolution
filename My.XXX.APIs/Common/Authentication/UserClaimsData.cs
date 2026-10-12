using System;
using System.Collections.Generic;

namespace My.XXX.APIs.Common.Authentication;

/// <summary>JWT 用户声明的内部载荷；字段名须兼容已签发的令牌。</summary>
public sealed class UserClaimsData
{
    public List<Guid> RoleIds { get; set; } = new();
}
