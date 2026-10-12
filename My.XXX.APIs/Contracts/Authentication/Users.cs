using My.XXX.APIs.Contracts.Menus;
using System;
using System.Collections.Generic;

namespace My.XXX.APIs.Contracts.Authentication;

public class UserInfoResponse
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public List<string> Roles { get; set; }
    public List<Guid> RoleIds { get; set; }
    public List<MenuTreeResponse> Menus { get; set; }
    public string Email { get; set; }
}
