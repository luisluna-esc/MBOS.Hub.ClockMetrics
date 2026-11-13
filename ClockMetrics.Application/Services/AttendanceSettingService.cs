using System.Net;
using ClockMetrics.Application.Attendance;
using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Application.Interfaces.Attendance;
using ClockMetrics.Core.Entities.Attendance;
using ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;
using ClockMetrics.Core.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ClockMetrics.Application.Services;

// Horario y reglas del cálculo. Los días se calculan cada vez que se consultan, así que un cambio
// aquí se refleja en todos los períodos abiertos (y también en los cerrados: para conservar un
// período tal como se cerró, conviene no cambiar las reglas a mitad de año).
public class AttendanceSettingService : IAttendanceSettingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AttendanceEngine _engine;
    private readonly IValidator<AttendanceSettingsDto> _validator;

    public AttendanceSettingService(IUnitOfWork unitOfWork, AttendanceEngine engine, IValidator<AttendanceSettingsDto> validator)
    {
        _unitOfWork = unitOfWork;
        _engine = engine;
        _validator = validator;
    }

    public async Task<ResponseGetObject> Get()
    {
        var settings = await _engine.GetSettingsAsync();
        var schedules = await _unitOfWork.Repository<WorkSchedule>().Query().OrderBy(s => s.DayOfWeek).ToListAsync();

        return Responses.Ok(new AttendanceSettingsDto
        {
            ToleranceMinutes = settings.ToleranceMinutes,
            DuplicateWindowMinutes = settings.DuplicateWindowMinutes,
            MinBreakMinutes = settings.MinBreakMinutes,
            MiddayFrom = AttendanceCalculator.Format(settings.MiddayFrom),
            AfternoonExitFrom = AttendanceCalculator.Format(settings.AfternoonExitFrom),
            EarliestNormalTime = AttendanceCalculator.Format(settings.EarliestNormalTime),
            LatestNormalTime = AttendanceCalculator.Format(settings.LatestNormalTime),
            PeriodCutoffDay = settings.PeriodCutoffDay,
            Schedules = Enumerable.Range(1, 7).Select(day =>
            {
                var s = schedules.FirstOrDefault(x => x.DayOfWeek == day);
                return new WorkScheduleDto
                {
                    DayOfWeek = day,
                    IsWorkingDay = s?.IsWorkingDay ?? false,
                    StartTime = Format(s?.StartTime),
                    BreakStart = Format(s?.BreakStart),
                    BreakEnd = Format(s?.BreakEnd),
                    EndTime = Format(s?.EndTime)
                };
            }).ToList()
        });
    }

    public async Task<ResponsePost> Update(AttendanceSettingsDto request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
            return Responses.PostError(HttpStatusCode.BadRequest, validation.Errors.Select(e => e.ErrorMessage).Distinct().ToArray());

        var settingRepository = _unitOfWork.Repository<AttendanceSetting>();
        var settings = await settingRepository.GetByIdAsync(1);
        var isNew = settings is null;
        settings ??= new AttendanceSetting { AttendanceSettingId = 1 };

        settings.ToleranceMinutes = request.ToleranceMinutes;
        settings.DuplicateWindowMinutes = request.DuplicateWindowMinutes;
        settings.MinBreakMinutes = request.MinBreakMinutes;
        settings.MiddayFrom = AttendanceService.ParseTime(request.MiddayFrom)!.Value;
        settings.AfternoonExitFrom = AttendanceService.ParseTime(request.AfternoonExitFrom)!.Value;
        settings.EarliestNormalTime = AttendanceService.ParseTime(request.EarliestNormalTime)!.Value;
        settings.LatestNormalTime = AttendanceService.ParseTime(request.LatestNormalTime)!.Value;
        settings.PeriodCutoffDay = request.PeriodCutoffDay;
        if (isNew) await settingRepository.AddAsync(settings);
        else settingRepository.Update(settings);

        var scheduleRepository = _unitOfWork.Repository<WorkSchedule>();
        var schedules = await scheduleRepository.Query().ToListAsync();
        foreach (var day in request.Schedules)
        {
            var schedule = schedules.FirstOrDefault(s => s.DayOfWeek == day.DayOfWeek);
            var isNewDay = schedule is null;
            schedule ??= new WorkSchedule { DayOfWeek = day.DayOfWeek };
            schedule.IsWorkingDay = day.IsWorkingDay;
            schedule.StartTime = day.IsWorkingDay ? AttendanceService.ParseTime(day.StartTime) : null;
            schedule.BreakStart = day.IsWorkingDay ? AttendanceService.ParseTime(day.BreakStart) : null;
            schedule.BreakEnd = day.IsWorkingDay ? AttendanceService.ParseTime(day.BreakEnd) : null;
            schedule.EndTime = day.IsWorkingDay ? AttendanceService.ParseTime(day.EndTime) : null;
            if (isNewDay) await scheduleRepository.AddAsync(schedule);
            else scheduleRepository.Update(schedule);
        }

        await _unitOfWork.SaveChangesAsync();
        return Responses.Saved(1, "Horario y reglas guardados.");
    }

    private static string? Format(TimeOnly? time) => time is { } t ? AttendanceCalculator.Format(t) : null;
}
