using System.Net;
using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Application.Interfaces.Attendance;
using ClockMetrics.Core.Entities.Attendance;
using ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;
using ClockMetrics.Core.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ClockMetrics.Application.Services;

public class WorkGroupService : IWorkGroupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<WorkGroupDto> _validator;

    public WorkGroupService(IUnitOfWork unitOfWork, IValidator<WorkGroupDto> validator)
    {
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAll()
    {
        var employees = _unitOfWork.Repository<Employee>().Query();
        var groups = await _unitOfWork.Repository<WorkGroup>().Query()
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.Name)
            .Select(g => new WorkGroupDto
            {
                WorkGroupId = g.WorkGroupId,
                Name = g.Name,
                DisplayOrder = g.DisplayOrder,
                IsActive = g.IsActive,
                EmployeeCount = employees.Count(e => e.WorkGroupId == g.WorkGroupId)
            })
            .ToListAsync();
        return Responses.Ok(groups);
    }

    public async Task<ResponsePost> Insert(WorkGroupDto request)
    {
        var errors = await Validate(request, null);
        if (errors.Length > 0)
            return Responses.PostError(HttpStatusCode.BadRequest, errors);

        var group = new WorkGroup { Name = request.Name.Trim(), DisplayOrder = request.DisplayOrder, IsActive = request.IsActive, CreatedAt = DateTime.UtcNow };
        await _unitOfWork.Repository<WorkGroup>().AddAsync(group);
        await _unitOfWork.SaveChangesAsync();
        return Responses.Saved(group.WorkGroupId, "Grupo creado.", HttpStatusCode.Created);
    }

    public async Task<ResponsePost> Update(long id, WorkGroupDto request)
    {
        var errors = await Validate(request, id);
        if (errors.Length > 0)
            return Responses.PostError(HttpStatusCode.BadRequest, errors);

        var repository = _unitOfWork.Repository<WorkGroup>();
        var group = await repository.GetByIdAsync(id);
        if (group is null)
            return Responses.PostError(HttpStatusCode.NotFound, "No se encontró el grupo.");

        group.Name = request.Name.Trim();
        group.DisplayOrder = request.DisplayOrder;
        group.IsActive = request.IsActive;
        repository.Update(group);
        await _unitOfWork.SaveChangesAsync();
        return Responses.Saved(id, "Grupo actualizado.");
    }

    public async Task<ResponsePost> Delete(long id)
    {
        var repository = _unitOfWork.Repository<WorkGroup>();
        var group = await repository.GetByIdAsync(id);
        if (group is null)
            return Responses.PostError(HttpStatusCode.NotFound, "No se encontró el grupo.");
        if (await _unitOfWork.Repository<Employee>().Query().AnyAsync(e => e.WorkGroupId == id))
            return Responses.PostError(HttpStatusCode.Conflict, "Este grupo tiene empleados y no se puede eliminar. Desactívalo en su lugar.");

        repository.Remove(group);
        await _unitOfWork.SaveChangesAsync();
        return Responses.Saved(id, "Grupo eliminado.");
    }

    private async Task<string[]> Validate(WorkGroupDto request, long? id)
    {
        var validation = await _validator.ValidateAsync(request);
        var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
        var name = request.Name.Trim();
        if (await _unitOfWork.Repository<WorkGroup>().Query().AnyAsync(g => g.Name == name && g.WorkGroupId != id))
            errors.Add("Ya existe un grupo con ese nombre.");
        return errors.ToArray();
    }
}
