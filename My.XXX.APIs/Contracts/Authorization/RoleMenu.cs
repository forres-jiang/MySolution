using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace My.XXX.APIs.Contracts.Authorization;

public class RoleMenuQuery
{
    [Required]
    public Guid RoleId { get; set; }
}

public class RoleMenuSelectionRequestBase
{
    [Required]
    public Guid RoleId { get; set; }

    [Required]
    public bool? Checked { get; set; }
}

public class SetRoleMenuCheckedRequest : RoleMenuSelectionRequestBase
{
    [Range(1, int.MaxValue)]
    [Required]
    public int MenuId { get; set; }
}

public class SetRoleMenusCheckedRequest : RoleMenuSelectionRequestBase
{
    [Required]
    public List<int> MenuIds { get; set; }
}

public class ReplaceRoleMenusRequest : RoleMenuQuery
{
    [Required]
    public List<int> MenuIds { get; set; }
}

public class RemoveRoleMenuRequest : RoleMenuQuery
{
    [Required]
    public int MenuId { get; set; }
}
