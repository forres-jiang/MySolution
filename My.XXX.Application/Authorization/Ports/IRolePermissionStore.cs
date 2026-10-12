using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace My.XXX.Application.Authorization.Ports;

public interface IRolePermissionStore
{
    Task<List<string>> GetAsync(Guid roleId, CancellationToken cancellationToken = default);
}
