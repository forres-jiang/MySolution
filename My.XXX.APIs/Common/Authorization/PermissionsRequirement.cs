using Microsoft.AspNetCore.Authorization;

namespace My.XXX.APIs.Common.Authorization;

public sealed class PermissionsRequirement(string policyName) : IAuthorizationRequirement
{
    public string Name { get; } = policyName;
}
