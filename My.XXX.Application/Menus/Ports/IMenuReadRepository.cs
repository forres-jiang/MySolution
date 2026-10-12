using My.XXX.Application.Abstractions;
using My.XXX.Application.Menus.Models;
using My.XXX.Domain.Menus.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Application.Menus.Ports;

public interface IMenuReadRepository
{
    Task<MenuState?> Get(int id, CancellationToken cancellationToken = default);
    Task<Paged<MenuState>> Search(MenuSearch query, CancellationToken cancellationToken = default);
    Task<List<MenuState>> GetMenus(int? parentId = null, List<int>? ids = null, bool? isDisplay = null, CancellationToken cancellationToken = default);
    Task<List<MenuState>> GetRoleMenuByRoles(List<Guid> roleIds, CancellationToken cancellationToken = default);
}
