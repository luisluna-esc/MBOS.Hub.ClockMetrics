using System.Net;
using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Application.Interfaces.Attendance;
using ClockMetrics.Core.Entities.Attendance;
using ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;
using ClockMetrics.Core.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ClockMetrics.Application.Services;

public class HolidayService : IHolidayService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<HolidayDto> _validator;

    public HolidayService(IUnitOfWork unitOfWork, IValidator<HolidayDto> validator)
    {
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAll(int? year)
    {
        var query = _unitOfWork.Repository<Holiday>().Query();
        if (year.HasValue)
            query = query.Where(h => h.Date.Year == year.Value);

        var holidays = await query.OrderBy(h => h.Date)
            .Select(h => new HolidayDto { HolidayId = h.HolidayId, Date = h.Date, Name = h.Name })
            .ToListAsync();
        return Responses.Ok(holidays);
    }

    public async Task<ResponsePost> Insert(HolidayDto request)
    {
        var errors = await Validate(request, null);
        if (errors.Length > 0)
            return Responses.PostError(HttpStatusCode.BadRequest, errors);

        var holiday = new Holiday { Date = request.Date, Name = request.Name.Trim() };
        await _unitOfWork.Repository<Holiday>().AddAsync(holiday);
        await _unitOfWork.SaveChangesAsync();
        return Responses.Saved(holiday.HolidayId, "Feriado creado.", HttpStatusCode.Created);
    }

    public async Task<ResponsePost> Update(long id, HolidayDto request)
    {
        var errors = await Validate(request, id);
        if (errors.Length > 0)
            return Responses.PostError(HttpStatusCode.BadRequest, errors);

        var repository = _unitOfWork.Repository<Holiday>();
        var holiday = await repository.GetByIdAsync(id);
        if (holiday is null)
            return Responses.PostError(HttpStatusCode.NotFound, "No se encontró el feriado.");

        holiday.Date = request.Date;
        holiday.Name = request.Name.Trim();
        repository.Update(holiday);
        await _unitOfWork.SaveChangesAsync();
        return Responses.Saved(id, "Feriado actualizado.");
    }

    public async Task<ResponsePost> Delete(long id)
    {
        var repository = _unitOfWork.Repository<Holiday>();
        var holiday = await repository.GetByIdAsync(id);
        if (holiday is null)
            return Responses.PostError(HttpStatusCode.NotFound, "No se encontró el feriado.");
        if (await IsClosed(holiday.Date))
            return Responses.PostError(HttpStatusCode.Conflict, "Ese feriado cae en un período cerrado.");

        repository.Remove(holiday);
        await _unitOfWork.SaveChangesAsync();
        return Responses.Saved(id, "Feriado eliminado.");
    }

    private async Task<string[]> Validate(HolidayDto request, long? id)
    {
        var validation = await _validator.ValidateAsync(request);
        var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
        if (await _unitOfWork.Repository<Holiday>().Query().AnyAsync(h => h.Date == request.Date && h.HolidayId != id))
            errors.Add("Ya hay un feriado en esa fecha.");
        // Un feriado cambia los atrasos y faltas de ese día: en un período cerrado no se toca.
        if (await IsClosed(request.Date))
            errors.Add("Esa fecha cae en un período cerrado.");
        return errors.ToArray();
    }

    private Task<bool> IsClosed(DateOnly date) =>
        _unitOfWork.Repository<AttendancePeriod>().Query().AnyAsync(p => p.IsClosed && p.StartDate <= date && p.EndDate >= date);
}
