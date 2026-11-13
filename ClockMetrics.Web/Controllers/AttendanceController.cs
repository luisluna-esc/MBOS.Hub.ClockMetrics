using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Application.Dtos.Reports;
using ClockMetrics.Application.Interfaces.Attendance;
using ClockMetrics.Core.Constants;
using ClockMetrics.Core.QueryFilters.Attendance;
using ClockMetrics.Core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClockMetrics.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;
    private readonly IAttendanceImportService _importService;
    private readonly IAttendanceReportService _reportService;

    public AttendanceController(IAttendanceService attendanceService, IAttendanceImportService importService, IAttendanceReportService reportService)
    {
        _attendanceService = attendanceService;
        _importService = importService;
        _reportService = reportService;
    }

    /// <summary>Sube el archivo que exporta el biométrico (.xls o .xlsx).</summary>
    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost("imports")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile file)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var result = await _importService.Import(stream.ToArray(), Path.GetFileName(file.FileName));
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("imports")]
    public async Task<IActionResult> GetImports([FromQuery] PaginationQueryFilter paginationQueryFilter)
    {
        var result = await _importService.GetImports(paginationQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    /// <summary>Registro jornada laboral de un empleado en un período.</summary>
    [HttpGet("periods/{periodId}/employees/{employeeId}")]
    public async Task<IActionResult> GetEmployeeMonth(long periodId, long employeeId)
    {
        var result = await _attendanceService.GetEmployeeMonth(periodId, employeeId);
        return StatusCode((int)result.StatusCode, result);
    }

    /// <summary>Registro jornada laboral de todos los empleados del filtro.</summary>
    [HttpGet("periods/{periodId}/records")]
    public async Task<IActionResult> GetRecords(long periodId, [FromQuery] AttendanceQueryFilter filter)
    {
        var result = await _attendanceService.GetRecords(periodId, filter);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("periods/{periodId}/summary")]
    public async Task<IActionResult> GetSummary(long periodId, [FromQuery] AttendanceQueryFilter filter)
    {
        var result = await _attendanceService.GetSummary(periodId, filter);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("periods/{periodId}/issues")]
    public async Task<IActionResult> GetIssues(long periodId, [FromQuery] AttendanceQueryFilter filter, [FromQuery] bool includeReviewed = false)
    {
        var result = await _attendanceService.GetIssues(periodId, filter, includeReviewed);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("accumulated")]
    public async Task<IActionResult> GetAccumulated([FromQuery] long fromPeriodId, [FromQuery] long toPeriodId, [FromQuery] AttendanceQueryFilter filter)
    {
        var result = await _attendanceService.GetAccumulated(fromPeriodId, toPeriodId, filter);
        return StatusCode((int)result.StatusCode, result);
    }

    /// <summary>Corrige o justifica un día (date en formato yyyy-MM-dd).</summary>
    [Authorize(Roles = RoleNames.Writers)]
    [HttpPut("employees/{employeeId}/days/{date}")]
    public async Task<IActionResult> SaveAdjustment(long employeeId, DateOnly date, [FromBody] AdjustmentRequestDto request)
    {
        var result = await _attendanceService.SaveAdjustment(employeeId, date, request);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpDelete("employees/{employeeId}/days/{date}")]
    public async Task<IActionResult> DeleteAdjustment(long employeeId, DateOnly date)
    {
        var result = await _attendanceService.DeleteAdjustment(employeeId, date);
        return StatusCode((int)result.StatusCode, result);
    }

    /// <summary>Registro jornada laboral: un empleado (employeeId) o todos los del filtro, una hoja por empleado.</summary>
    [HttpGet("periods/{periodId}/reports/employees/{format}")]
    public async Task<IActionResult> EmployeeReport(long periodId, ReportFormat format, [FromQuery] AttendanceQueryFilter filter) =>
        ToFile(await _reportService.EmployeeReport(periodId, filter, format));

    [HttpGet("periods/{periodId}/reports/summary/{format}")]
    public async Task<IActionResult> MonthSummaryReport(long periodId, ReportFormat format, [FromQuery] AttendanceQueryFilter filter) =>
        ToFile(await _reportService.MonthSummaryReport(periodId, filter, format));

    [HttpGet("reports/accumulated/{format}")]
    public async Task<IActionResult> AccumulatedReport(ReportFormat format, [FromQuery] long fromPeriodId, [FromQuery] long toPeriodId, [FromQuery] AttendanceQueryFilter filter) =>
        ToFile(await _reportService.AccumulatedReport(fromPeriodId, toPeriodId, filter, format));

    private IActionResult ToFile(ReportResult result) =>
        result.File is { } file
            ? File(file.Content, file.ContentType, file.FileName)
            : StatusCode((int)result.Error!.StatusCode, result.Error);
}
