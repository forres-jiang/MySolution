using My.XXX.Application.Abstractions.Ports;
using My.XXX.Application.Abstractions.Results;
using My.XXX.Application.Authorization.Ports;
using My.XXX.Domain.Abstractions;
using My.XXX.Domain.Authentication.Models;
using My.XXX.Domain.Authorization.Policies;
using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Application.Authorization;

public sealed class UseCaseAuthorization(ICurrentUser current, IPermissionQuery permissions)
{
    public async Task<Result<UserIdentity>> RequireAnyAsync(CancellationToken cancellationToken, params string[] codes)
    {
        var user = current.User;
        if (user == null || string.IsNullOrWhiteSpace(user.UserId))
        {
            return Result.Fail<UserIdentity>(new BusinessError("Authentication required.", "Authentication.Required", BusinessErrorKind.Unauthorized).ToError());
        }

        if (codes.Length == 0 || codes.Any(code => !PermissionCodes.AllSet.Contains(code)))
        {
            return Denied();
        }

        if (user.RoleIds.Count > 0)
        {
            var granted = await permissions.GetPermissionCodesAsync(user.RoleIds.ToList(), user.UserId, cancellationToken);
            if (codes.Any(code => granted.Contains(code, StringComparer.Ordinal)))
            {
                return Result.Ok(user);
            }
        }

        return Denied();
    }

    private static Result<UserIdentity> Denied()
    {
        return Result.Fail<UserIdentity>(new BusinessError("Permission denied.", "Authorization.Forbidden", BusinessErrorKind.Forbidden).ToError());
    }
}