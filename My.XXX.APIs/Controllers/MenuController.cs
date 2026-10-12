using My.XXX.APIs.Common.Authorization;
using My.XXX.APIs.Common.Mapping;
using My.XXX.APIs.Common.Responses;
using My.XXX.APIs.Contracts.Authorization;
using My.XXX.APIs.Contracts.Menus;
using My.XXX.APIs.Contracts.Shared;
using My.XXX.Application.Authorization;
using My.XXX.Application.Menus;
using My.XXX.Domain.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace My.XXX.APIs.Controllers;

[Authorize("Permissions")]
[ApiController]
[Route("api/[controller]")]
public sealed class MenuController(MenuCommandService commands, MenuQueryService queries, RoleMenuAssignmentService roles) : ControllerBase
{
    private static WireMapper Mapper => WireMapper.Instance;

    [HttpPost("add")]
    [RequiresPermission(PermissionCodes.MenuAdd)]
    public async Task<ActionResult<APIResponse<bool>>> Add(CreateMenuRequest model)
    {
        return (await commands.Add(Mapper.ToMenuCreation(model), HttpContext.RequestAborted)).ToHttpResult(HttpContext.TraceIdentifier);
    }

    [HttpDelete("remove")]
    [RequiresPermission(PermissionCodes.MenuRemove)]
    public async Task<ActionResult<APIResponse<bool>>> Remove(RemoveMenusRequest model)
    {
        return (await commands.Remove(model.MenuIds, HttpContext.RequestAborted)).ToHttpResult(HttpContext.TraceIdentifier);
    }

    [HttpPatch("edit")]
    [RequiresPermission(PermissionCodes.MenuEdit)]
    public async Task<ActionResult<APIResponse<bool>>> Edit(UpdateMenuRequest model)
    {
        return (await commands.Update(Mapper.ToMenuModification(model), HttpContext.RequestAborted)).ToHttpResult(HttpContext.TraceIdentifier);
    }

    [HttpGet("Get/{menuId}")]
    [RequiresPermission(PermissionCodes.MenuGet)]
    public async Task<ActionResult<APIResponse<MenuResponse>>> Get(int menuId)
    {
        var result = await queries.FindAsync(menuId, HttpContext.RequestAborted);
        return result.ToMappedHttpResult(menu => Mapper.ToMenuResponse(menu)!, HttpContext.TraceIdentifier);
    }

    [HttpPost("List")]
    [RequiresPermission(PermissionCodes.MenuList)]
    public async Task<ActionResult<APIResponse<PagedResponse<MenuResponse>>>> List(MenuQuery query)
    {
        var result = await queries.GetMenus(Mapper.ToListingQuery(query), HttpContext.RequestAborted);
        return result.ToMappedHttpResult(page => PagedResponse<MenuResponse>.Create(
            page.List.Select(m => Mapper.ToMenuResponse(m)!).ToList(), page.Total), HttpContext.TraceIdentifier);
    }

    [HttpPost("Search")]
    [RequiresPermission(PermissionCodes.MenuSearch)]
    public async Task<ActionResult<APIResponse<PagedResponse<MenuSearchPickerResponse>>>> Search(MenuQuery query)
    {
        var result = await queries.SearchMenus(Mapper.ToListingQuery(query), HttpContext.RequestAborted);
        return result.ToMappedHttpResult(page => PagedResponse<MenuSearchPickerResponse>.Create(
            page.List.Select(m => Mapper.ToMenuSearchPicker(m)!).ToList(), page.Total), HttpContext.TraceIdentifier);
    }

    [HttpPost("RoleMenu")]
    [RequiresPermission(PermissionCodes.MenuRoleMenu)]
    public async Task<ActionResult<APIResponse<bool>>> RoleMenu(SetRoleMenuCheckedRequest model)
    {
        var result = await roles.SetRoleMenusChecked(model.RoleId, new() { model.MenuId }, model.Checked, HttpContext.RequestAborted);
        return result.ToHttpResult(HttpContext.TraceIdentifier);
    }

    [HttpPost("RoleMenus")]
    [RequiresPermission(PermissionCodes.MenuRoleMenus)]
    public async Task<ActionResult<APIResponse<bool>>> RoleMenus(ReplaceRoleMenusRequest model)
    {
        return (await roles.ReplaceRoleMenus(model.RoleId, model.MenuIds, HttpContext.RequestAborted)).ToHttpResult(HttpContext.TraceIdentifier);
    }

    [HttpPost("RoleMenuChecked")]
    [RequiresPermission(PermissionCodes.MenuRoleMenuChecked)]
    public async Task<ActionResult<APIResponse<bool>>> RoleMenuChecked(SetRoleMenusCheckedRequest model)
    {
        var result = await roles.SetRoleMenusChecked(model.RoleId, model.MenuIds, model.Checked, HttpContext.RequestAborted);
        return result.ToHttpResult(HttpContext.TraceIdentifier);
    }

    [HttpPost("RemoveRoleMenu")]
    [RequiresPermission(PermissionCodes.MenuRemoveRoleMenu)]
    public async Task<ActionResult<APIResponse<bool>>> RemoveRoleMenu(RemoveRoleMenuRequest rrm)
    {
        return (await roles.RemoveRoleMenus(rrm.RoleId, new() { rrm.MenuId }, HttpContext.RequestAborted)).ToHttpResult(HttpContext.TraceIdentifier);
    }

    [HttpPost("Tree")]
    [RequiresPermission(PermissionCodes.MenuTree)]
    public async Task<ActionResult<APIResponse<List<MenuTreeResponse>>>> Tree()
    {
        return (await queries.GetTreeMenus(null, HttpContext.RequestAborted)).ToMappedHttpResult(menus => Mapper.ToMenuTreeResponses(menus), HttpContext.TraceIdentifier);
    }

    [HttpPost("DisplayTree")]
    [RequiresPermission(PermissionCodes.MenuDisplayTree)]
    public async Task<ActionResult<APIResponse<List<MenuTreeResponse>>>> DisplayTree()
    {
        return (await queries.GetTreeMenus(true, HttpContext.RequestAborted)).ToMappedHttpResult(menus => Mapper.ToMenuTreeResponses(menus), HttpContext.TraceIdentifier);
    }

    [HttpPost("TreeByRoleId")]
    [RequiresPermission(PermissionCodes.MenuTreeByRoleId)]
    public async Task<ActionResult<APIResponse<List<MenuTreeResponse>>>> GetMenuTreeByRoleId(RoleMenuQuery model)
    {
        return (await queries.GetMenuTreeCheckedByRole(model.RoleId, HttpContext.RequestAborted)).ToMappedHttpResult(menus => Mapper.ToMenuTreeResponses(menus), HttpContext.TraceIdentifier);
    }

    [HttpPost("TreeByRoleIds")]
    [RequiresPermission(PermissionCodes.MenuTreeByRoleIds)]
    public async Task<ActionResult<APIResponse<List<MenuTreeResponse>>>> GetMenuTreeByRoleIds(RolesMenuQuery model)
    {
        return (await queries.GetMenuTreeCheckedByRoles(model.Ids, HttpContext.RequestAborted)).ToMappedHttpResult(menus => Mapper.ToMenuTreeResponses(menus), HttpContext.TraceIdentifier);
    }

    [HttpPatch("UpdateSort")]
    [RequiresPermission(PermissionCodes.MenuUpdateSort)]
    public async Task<ActionResult<APIResponse<bool>>> UpdateSort(ReorderMenuRequest model)
    {
        return (await commands.UpdateSort(Mapper.ToReorderCommand(model), HttpContext.RequestAborted)).ToHttpResult(HttpContext.TraceIdentifier);
    }
}
