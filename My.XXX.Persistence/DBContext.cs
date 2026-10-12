using My.XXX.Persistence.PersistenceModels;
using LinqToDB;
using LinqToDB.Data;

namespace My.XXX.Persistence;

public class DBContext : DataConnection
{
    public DBContext(DataOptions<DBContext> dataOption) : base(dataOption.Options)
    {
    }
    // 仅供持久化层内部的仓储/事务使用；对外只经端口暴露领域模型，避免 PO 泄漏到组合根。
    internal ITable<Menus> Menus => this.GetTable<Menus>();
    internal ITable<RoleMenu> RoleMenu => this.GetTable<RoleMenu>();
}
