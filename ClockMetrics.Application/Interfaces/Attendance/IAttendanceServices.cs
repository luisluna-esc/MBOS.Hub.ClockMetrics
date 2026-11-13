using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Application.Dtos.Reports;
using ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;
using ClockMetrics.Core.QueryFilters.Attendance;
using ClockMetrics.Core.QueryFilters.Pagination;

namespace ClockMetrics.Application.Interfaces.Attendance;

public interface IAttendanceImportService
{
    Task<ResponseGetObject> Import(byte[] content, string fileName);
    Task<ResponseGetObject> GetImports(PaginationQueryFilter paginationQueryFilter);
}

public interface IAttendanceService
{
    Task<ResponseGetObject> GetEmployeeMonth(long periodId, long employeeId);
    Task<ResponseGetObject> GetRecords(long periodId, AttendanceQueryFilter filter);
    Task<ResponseGetObject> GetSummary(long periodId, AttendanceQueryFilter filter);
    Task<ResponseGetObject> GetIssues(long periodId, AttendanceQueryFilter filter, bool includeReviewed);
    Task<ResponseGetObject> GetAccumulated(long fromPeriodId, long toPeriodId, AttendanceQueryFilter filter);
    Task<ResponsePost> SaveAdjustment(long employeeId, DateOnly date, AdjustmentRequestDto request);
    Task<ResponsePost> DeleteAdjustment(long employeeId, DateOnly date);
}

public interface IEmployeeService
{
    Task<ResponseGetObject> GetAll(PaginationQueryFilter paginationQueryFilter, EmployeeQueryFilter filter);
    Task<ResponsePost> Update(long id, EmployeeRequestDto request);
}

public interface IWorkGroupService
{
    Task<ResponseGetObject> GetAll();
    Task<ResponsePost> Insert(WorkGroupDto request);
    Task<ResponsePost> Update(long id, WorkGroupDto request);
    Task<ResponsePost> Delete(long id);
}

public interface IHolidayService
{
    Task<ResponseGetObject> GetAll(int? year);
    Task<ResponsePost> Insert(HolidayDto request);
    Task<ResponsePost> Update(long id, HolidayDto request);
    Task<ResponsePost> Delete(long id);
}

public interface IAttendancePeriodService
{
    Task<ResponseGetObject> GetAll(int? year);
    Task<ResponsePost> Insert(PeriodRequestDto request);
    Task<ResponsePost> Update(long id, PeriodRequestDto request);
    Task<ResponsePost> SetClosed(long id, bool closed);
}

public interface IAttendanceSettingService
{
    Task<ResponseGetObject> Get();
    Task<ResponsePost> Update(AttendanceSettingsDto request);
}

public interface IAttendanceReportService
{
    Task<ReportResult> EmployeeReport(long periodId, AttendanceQueryFilter filter, ReportFormat format);
    Task<ReportResult> MonthSummaryReport(long periodId, AttendanceQueryFilter filter, ReportFormat format);
    Task<ReportResult> AccumulatedReport(long fromPeriodId, long toPeriodId, AttendanceQueryFilter filter, ReportFormat format);
}
