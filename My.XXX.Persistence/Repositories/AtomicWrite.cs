using LinqToDB.Data;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Persistence.Repositories;

/// <summary>以原子方式执行一次仓储写操作；意外异常将向上传播。</summary>
internal static class AtomicWrite
{
    public static async Task<T> ExecuteAsync<T>(DataConnection connection, Func<Task<T>> action,
        Func<T, bool> succeeded, CancellationToken cancellationToken = default)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action();
            cancellationToken.ThrowIfCancellationRequested();
            if (succeeded(result)) await transaction.CommitAsync(cancellationToken);
            else await transaction.RollbackAsync(CancellationToken.None);
            return result;
        }
        catch
        {
            // HTTP 请求被取消后仍必须执行清理。
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
