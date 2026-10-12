using Microsoft.AspNetCore.Authorization;
using System;
namespace My.XXX.APIs.Common.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequiresPermissionAttribute(string code) : AuthorizeAttribute("Permissions")
{
    public string Code { get; } = code;
}
