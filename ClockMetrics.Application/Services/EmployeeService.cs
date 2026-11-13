using System.Net;
using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Application.Interfaces.Attendance;
using ClockMetrics.Core.Configuration;
using ClockMetrics.Core.Entities.Attendance;
using ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;
using ClockMetrics.Core.Interfaces;
using ClockMetrics.Core.QueryFilters.Attendance;
using ClockMetrics.Core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ClockMetrics.Application.Services;

// Los empleados los crea la importación del biométrico (por AC-No). Aquí solo se ajusta lo que el
// reloj no sabe: el nombre para los reportes, el grupo y si sale en los reportes. No se eliminan:
// se desactivan, para no perder su historial de marcas.
public class EmployeeService : IEmployeeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<EmployeeRequestDto> _validator;

    public EmployeeService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<EmployeeRequestDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAll(PaginationQueryFilter paginationQueryFilter, EmployeeQueryFilter filter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = from e in _unitOfWork.Repository<Employee>().Query()
                    join g in _unitOfWork.Repository<WorkGroup>().Query() on e.WorkGroupId equals g.WorkGroupId into groups
                    from g in groups.DefaultIfEmpty()
                    select new { e, GroupName = g != null ? g.Name : null };

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            var search = paginationQueryFilter.SearchCriteria.Trim().ToLower();
            query = query.Where(x => x.e.FullName.ToLower().Contains(search) || x.e.DeviceName.ToLower().Contains(search) || x.e.DeviceCode == search);
        }
        if (filter.WorkGroupId.HasValue)
            query = query.Where(x => x.e.WorkGroupId == filter.WorkGroupId.Value);
        if (filter.IncludeInReports.HasValue)
            query = query.Where(x => x.e.IncludeInReports == filter.IncludeInReports.Value);
        if (filter.IsActive.HasValue)
            query = query.Where(x => x.e.IsActive == filter.IsActive.Value);

        var totalRecords = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.e.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new EmployeeDto
            {
                EmployeeId = x.e.EmployeeId,
                DeviceCode = x.e.DeviceCode,
                FullName = x.e.FullName,
                DeviceName = x.e.DeviceName,
                WorkGroupId = x.e.WorkGroupId,
                WorkGroupName = x.GroupName,
                IncludeInReports = x.e.IncludeInReports,
                IsActive = x.e.IsActive,
                CreatedAt = x.e.CreatedAt
            })
            .ToListAsync();

        return Responses.Ok(new PagedResult<EmployeeDto> { Items = items, TotalRecords = totalRecords, PageNumber = pageNumber, PageSize = pageSize });
    }

    public async Task<ResponsePost> Update(long id, EmployeeRequestDto request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
            return Responses.PostError(HttpStatusCode.BadRequest, validation.Errors.Select(e => e.ErrorMessage).ToArray());

        if (request.WorkGroupId.HasValue && !await _unitOfWork.Repository<WorkGroup>().Query().AnyAsync(g => g.WorkGroupId == request.WorkGroupId.Value))
            return Responses.PostError(HttpStatusCode.BadRequest, "El grupo no existe.");

        var repository = _unitOfWork.Repository<Employee>();
        var employee = await repository.GetByIdAsync(id);
        if (employee is null)
            return Responses.PostError(HttpStatusCode.NotFound, "No se encontró el empleado.");

        employee.FullName = request.FullName.Trim();
        employee.WorkGroupId = request.WorkGroupId;
        employee.IncludeInReports = request.IncludeInReports;
        employee.IsActive = request.IsActive;
        repository.Update(employee);
        await _unitOfWork.SaveChangesAsync();

        return Responses.Saved(id, "Empleado actualizado.");
    }
}
