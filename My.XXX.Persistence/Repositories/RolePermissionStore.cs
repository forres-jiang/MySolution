using My.XXX.Application.Authorization.Ports;
using My.XXX.Persistence.PersistenceModels;
using LinqToDB;
using LinqToDB.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace My.XXX.Persistence.Repositories;

public sealed class RolePermissionStore(DBContext db) : IRolePermissionStore
{
    public Task<List<string>> GetAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        db.GetTable<RolePermission>().Where(p => p.RoleId == roleId).Select(p => p.Code).ToListAsync(cancellationToken);
}
