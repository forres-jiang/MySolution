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

/// <summary>同一作用域按用户与角色集合复用权限快照；后台调用应为每次工作创建独立作用域。</summary>
public sealed class UseCaseAuthorization(ICurrentUser current, IPermissionQuery permissions) : IDisposable
{
    private readonly SemaphoreSlim snapshotLock = new(1, 1);
    private PermissionSnapshot? snapshot;
    private long snapshotVersion;

    public async Task<Result<UserIdentity>> RequireAnyAsync(CancellationToken cancellationToken, params string[] codes)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = current.User;
        if (user == null || string.IsNullOrWhiteSpace(user.UserId))
        {
            InvalidatePermissions();
            return Result.Fail<UserIdentity>(new BusinessError("Authentication required.", "Authentication.Required", BusinessErrorKind.Unauthorized).ToError());
        }

        if (codes.Length == 0 || codes.Any(code => !PermissionCodes.AllSet.Contains(code)))
        {
            return Denied();
        }

        if (user.RoleIds.Count > 0)
        {
            var granted = await GetPermissionsAsync(user, cancellationToken);
            if (codes.Any(granted.Contains))
            {
                return Result.Ok(user);
            }
        }
        else
        {
            InvalidatePermissions();
        }

        return Denied();
    }

    // 成功修改权限后，当前作用域内的后续用例也必须重新授权。
    internal void InvalidatePermissions()
    {
        Interlocked.Increment(ref snapshotVersion);
    }

    private async Task<HashSet<string>> GetPermissionsAsync(UserIdentity user, CancellationToken cancellationToken)
    {
        var roleIds = user.RoleIds.Distinct().OrderBy(roleId => roleId).ToArray();
        await snapshotLock.WaitAsync(cancellationToken);
        try
        {
            var version = Interlocked.Read(ref snapshotVersion);
            if (snapshot is { } cached && cached.Version == version && cached.UserId == user.UserId && cached.RoleIds.SequenceEqual(roleIds))
            {
                return cached.Codes;
            }

            var codes = await permissions.GetPermissionCodesAsync(roleIds.ToList(), user.UserId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var granted = new HashSet<string>(codes, StringComparer.Ordinal);
            snapshot = new PermissionSnapshot(user.UserId, roleIds, granted, version);
            return granted;
        }
        finally
        {
            snapshotLock.Release();
        }
    }

    public void Dispose() => snapshotLock.Dispose();

    private sealed record PermissionSnapshot(string UserId, Guid[] RoleIds, HashSet<string> Codes, long Version);

    private static Result<UserIdentity> Denied()
    {
        return Result.Fail<UserIdentity>(new BusinessError("Permission denied.", "Authorization.Forbidden", BusinessErrorKind.Forbidden).ToError());
    }
}
