using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace My.XXX.APIs.Contracts.Authorization;

public class RolesMenuQuery
{
    [Required]
    public List<Guid> Ids { get; set; }
}

public class RoleResponse
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; }
    public string RoleType { get; set; }
}
