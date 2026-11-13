using ClockMetrics.Application.Dtos.Attendance;
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
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _service;

    public EmployeesController(IEmployeeService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter pagination, [FromQuery] EmployeeQueryFilter filter)
    {
        var result = await _service.GetAll(pagination, filter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] EmployeeRequestDto request)
    {
        var result = await _service.Update(id, request);
        return StatusCode((int)result.StatusCode, result);
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkGroupsController : ControllerBase
{
    private readonly IWorkGroupService _service;

    public WorkGroupsController(IWorkGroupService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAll();
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] WorkGroupDto request)
    {
        var result = await _service.Insert(request);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] WorkGroupDto request)
    {
        var result = await _service.Update(id, request);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _service.Delete(id);
        return StatusCode((int)result.StatusCode, result);
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HolidaysController : ControllerBase
{
    private readonly IHolidayService _service;

    public HolidaysController(IHolidayService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? year)
    {
        var result = await _service.GetAll(year);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] HolidayDto request)
    {
        var result = await _service.Insert(request);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] HolidayDto request)
    {
        var result = await _service.Update(id, request);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _service.Delete(id);
        return StatusCode((int)result.StatusCode, result);
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendancePeriodsController : ControllerBase
{
    private readonly IAttendancePeriodService _service;

    public AttendancePeriodsController(IAttendancePeriodService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? year)
    {
        var result = await _service.GetAll(year);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] PeriodRequestDto request)
    {
        var result = await _service.Insert(request);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] PeriodRequestDto request)
    {
        var result = await _service.Update(id, request);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost("{id}/close")]
    public async Task<IActionResult> Close(long id)
    {
        var result = await _service.SetClosed(id, true);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost("{id}/reopen")]
    public async Task<IActionResult> Reopen(long id)
    {
        var result = await _service.SetClosed(id, false);
        return StatusCode((int)result.StatusCode, result);
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceSettingsController : ControllerBase
{
    private readonly IAttendanceSettingService _service;

    public AttendanceSettingsController(IAttendanceSettingService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _service.Get();
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] AttendanceSettingsDto request)
    {
        var result = await _service.Update(request);
        return StatusCode((int)result.StatusCode, result);
    }
}
