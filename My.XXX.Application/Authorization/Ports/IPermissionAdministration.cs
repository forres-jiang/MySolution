using FluentResults;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace My.XXX.Application.Authorization.Ports;

public interface IPermissionAdministration
{
    Task<Result<List<string>>> GetAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<Result> ReplaceAsync(Guid roleId, List<string> codes, CancellationToken cancellationToken = default);
}
