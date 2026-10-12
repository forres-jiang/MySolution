using My.XXX.Application.Authorization.Ports;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Application.Authorization;

public sealed class PermissionQuery(IPermissionStore store) : IPermissionQuery
{
    public Task<List<string>> GetPermissionCodesAsync(List<Guid> roleIds, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roleIds);
        return store.GetPermissionCodesAsync(roleIds, cancellationToken);
    }
}
