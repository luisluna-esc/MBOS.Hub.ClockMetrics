using ClockMetrics.Application.Interfaces.Audit;
using ClockMetrics.Core.Constants;
using ClockMetrics.Core.QueryFilters.Audit;
using ClockMetrics.Core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClockMetrics.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.AuditViewers)]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] AuditLogQueryFilter auditLogQueryFilter)
    {
        var result = await _auditLogService.GetAllAuditLogs(paginationQueryFilter, auditLogQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }
}
