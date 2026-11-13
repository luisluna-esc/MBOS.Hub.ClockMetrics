using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Application.Services;
using ClockMetrics.Core.Constants;
using FluentValidation;

namespace ClockMetrics.Application.Validators.Attendance;

public class AdjustmentRequestValidator : AbstractValidator<AdjustmentRequestDto>
{
    public AdjustmentRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Escribe el motivo del ajuste.")
            .MaximumLength(500).WithMessage("El motivo no puede pasar de 500 caracteres.");

        RuleFor(x => x.JustificationType)
            .Must(t => string.IsNullOrWhiteSpace(t) || JustificationTypes.All.Contains(t))
            .WithMessage("El tipo de justificación no es válido.");

        RuleFor(x => x)
            .Must(x => x.OverrideMarks || !string.IsNullOrWhiteSpace(x.JustificationType) || x.ExcuseLateAm || x.ExcuseLatePm || x.ExcuseMissing)
            .WithMessage("Corrige las marcas o marca qué se justifica.");

        When(x => x.OverrideMarks, () =>
        {
            RuleFor(x => x)
                .Must(x => new[] { x.EntryAm, x.ExitAm, x.EntryPm, x.ExitPm }.All(t => string.IsNullOrWhiteSpace(t) || AttendanceService.ParseTime(t) is not null))
                .WithMessage("Las horas deben tener el formato HH:mm.");
            RuleFor(x => x)
                .Must(x =>
                {
                    var times = new[] { x.EntryAm, x.ExitAm, x.EntryPm, x.ExitPm }.Select(AttendanceService.ParseTime).Where(t => t.HasValue).ToList();
                    return times.SequenceEqual(times.OrderBy(t => t));
                })
                .WithMessage("Las horas deben ir en orden: entrada mañana, salida mañana, entrada tarde, salida tarde.");
        });
    }
}

public class EmployeeRequestValidator : AbstractValidator<EmployeeRequestDto>
{
    public EmployeeRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede pasar de 150 caracteres.");
    }
}

public class WorkGroupValidator : AbstractValidator<WorkGroupDto>
{
    public WorkGroupValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre no puede pasar de 50 caracteres.");
    }
}

public class HolidayValidator : AbstractValidator<HolidayDto>
{
    public HolidayValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("La fecha es obligatoria.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede pasar de 150 caracteres.");
    }
}

public class PeriodRequestValidator : AbstractValidator<PeriodRequestDto>
{
    public PeriodRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100).WithMessage("El año no es válido.");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithMessage("El mes no es válido.");
        RuleFor(x => x).Must(x => x.EndDate >= x.StartDate).WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleFor(x => x).Must(x => x.EndDate.DayNumber - x.StartDate.DayNumber <= 62).WithMessage("Un período no puede pasar de dos meses.");
    }
}

public class AttendanceSettingsValidator : AbstractValidator<AttendanceSettingsDto>
{
    public AttendanceSettingsValidator()
    {
        RuleFor(x => x.ToleranceMinutes).InclusiveBetween(0, 120).WithMessage("La tolerancia debe estar entre 0 y 120 minutos.");
        RuleFor(x => x.DuplicateWindowMinutes).InclusiveBetween(0, 60).WithMessage("El margen de marcas repetidas debe estar entre 0 y 60 minutos.");
        RuleFor(x => x.MinBreakMinutes).InclusiveBetween(0, 240).WithMessage("El receso mínimo debe estar entre 0 y 240 minutos.");
        RuleFor(x => x.PeriodCutoffDay).InclusiveBetween(1, 28).WithMessage("El día de corte debe estar entre 1 y 28.");
        RuleFor(x => x)
            .Must(x => new[] { x.MiddayFrom, x.AfternoonExitFrom, x.EarliestNormalTime, x.LatestNormalTime }.All(t => AttendanceService.ParseTime(t) is not null))
            .WithMessage("Las horas deben tener el formato HH:mm.");
        RuleFor(x => x.Schedules).Must(s => s.Count == 7 && s.Select(d => d.DayOfWeek).Distinct().Count() == 7)
            .WithMessage("Debe venir el horario de los 7 días.");
        RuleForEach(x => x.Schedules).ChildRules(day =>
        {
            day.RuleFor(d => d).Must(d => !d.IsWorkingDay || (AttendanceService.ParseTime(d.StartTime) is { } s && AttendanceService.ParseTime(d.EndTime) is { } e && e > s))
                .WithMessage("Un día laborable necesita hora de entrada y de salida, en ese orden.");
            day.RuleFor(d => d).Must(d =>
                {
                    var breakStart = AttendanceService.ParseTime(d.BreakStart);
                    var breakEnd = AttendanceService.ParseTime(d.BreakEnd);
                    if (breakStart is null && breakEnd is null) return true;
                    return breakStart is not null && breakEnd is not null && breakEnd > breakStart
                        && breakStart > AttendanceService.ParseTime(d.StartTime) && breakEnd < AttendanceService.ParseTime(d.EndTime);
                })
                .WithMessage("El receso necesita inicio y fin, dentro de la jornada (o ninguno de los dos para jornada continua).");
        });
    }
}
