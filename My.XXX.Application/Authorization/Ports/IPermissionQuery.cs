using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Application.Authorization.Ports;

public interface IPermissionQuery
{
    Task<List<string>> GetPermissionCodesAsync(List<Guid> roleIds, string userId, CancellationToken cancellationToken = default);
}
