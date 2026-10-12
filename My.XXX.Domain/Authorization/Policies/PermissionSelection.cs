using My.XXX.Domain.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace My.XXX.Domain.Authorization.Policies;

public static class PermissionSelection
{
    public static DomainResult Validate(Guid roleId, IReadOnlyCollection<string> codes) =>
        roleId == Guid.Empty || codes == null || codes.Any(code => !PermissionCodes.AllSet.Contains(code))
            ? DomainResult.Fail(new BusinessError("Unknown permission code or invalid role.", code: "Permission.InvalidInput"))
            : DomainResult.Ok();
}
