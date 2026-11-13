using ClockMetrics.Application.Dtos.Security;
using ClockMetrics.Application.Interfaces.Security;
using ClockMetrics.Core.Constants;
using ClockMetrics.Core.QueryFilters.Pagination;
using ClockMetrics.Core.QueryFilters.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClockMetrics.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Admin)]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IRoleMenuService _roleMenuService;

    public RolesController(IRoleService roleService, IRoleMenuService roleMenuService)
    {
        _roleService = roleService;
        _roleMenuService = roleMenuService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] RoleQueryFilter roleQueryFilter)
    {
        var result = await _roleService.GetAllRoles(paginationQueryFilter, roleQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] RoleDto roleDto)
    {
        var result = await _roleService.InsertRole(roleDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] RoleDto roleDto)
    {
        var result = await _roleService.UpdateRole(id, roleDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _roleService.DeleteRole(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{id}/menus")]
    public async Task<IActionResult> GetMenus(long id)
    {
        var result = await _roleMenuService.GetMenusByRole(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost("{id}/menus/{menuId}")]
    public async Task<IActionResult> AssignMenu(long id, long menuId)
    {
        var result = await _roleMenuService.AssignMenu(id, menuId);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpDelete("{id}/menus/{menuId}")]
    public async Task<IActionResult> RemoveMenu(long id, long menuId)
    {
        var result = await _roleMenuService.RemoveMenu(id, menuId);
        return StatusCode((int)result.StatusCode, result);
    }
}
