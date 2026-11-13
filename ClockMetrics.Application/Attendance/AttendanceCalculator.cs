using ClockMetrics.Core.Constants;
using ClockMetrics.Core.Entities.Attendance;

namespace ClockMetrics.Application.Attendance;

// Arma un día a partir de las marcas del biométrico, con las mismas reglas que el informe manual
// "Registro jornada laboral":
//   - Dos marcas a menos de DuplicateWindowMinutes son una sola (se presionó dos veces).
//   - Cada marca va a su casilla por la hora: antes de MiddayFrom es la entrada de la mañana,
//     desde AfternoonExitFrom la salida de la tarde, y en medio la salida al receso y el regreso.
//     En jornada continua (viernes) la primera es la entrada y la última la salida.
//   - Atraso: pasada la tolerancia, se cuenta desde la hora de entrada (08:06 = 6 min; 08:05 = 0).
//   - Horas trabajadas: salida − entrada − el receso del horario (no el real), solo si hay ambas.
// Es una función pura: no lee la base, así se prueba sola y los reportes calculan igual que la pantalla.
public static class AttendanceCalculator
{
    public static DayResult Calculate(
        DateOnly date,
        string rawMarks,
        WorkSchedule? schedule,
        string? holidayName,
        AttendanceSetting settings,
        AttendanceAdjustment? adjustment)
    {
        var marks = ParseMarks(rawMarks);
        var result = new DayResult
        {
            Date = date,
            DayOfWeek = date.DayOfWeek == System.DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek,
            RawMarks = string.Join(' ', marks.Select(Format)),
            HolidayName = holidayName,
            IsContinuous = schedule?.IsContinuous ?? false
        };
        ApplyAdjustmentInfo(result, adjustment);

        if (schedule is null || !schedule.IsWorkingDay || holidayName is not null)
        {
            result.Status = holidayName is not null ? DayStatuses.Holiday : DayStatuses.NonWorking;
            if (marks.Count > 0)
                result.Issues.Add(DayIssues.MarksOnNonWorkingDay);
            return result;
        }

        var start = schedule.StartTime ?? new TimeOnly(8, 0);
        var end = schedule.EndTime ?? start;

        if (adjustment is { OverrideMarks: true })
        {
            result.EntryAm = adjustment.EntryAm;
            result.ExitAm = result.IsContinuous ? null : adjustment.ExitAm;
            result.EntryPm = result.IsContinuous ? null : adjustment.EntryPm;
            result.ExitPm = adjustment.ExitPm;
        }
        else
        {
            var kept = RemoveDuplicates(marks, settings.DuplicateWindowMinutes, out var hadDuplicates);
            if (hadDuplicates)
                result.Issues.Add(DayIssues.Duplicate);

            var extra = result.IsContinuous
                ? AssignContinuous(result, kept, start, end)
                : AssignSplit(result, kept, schedule.BreakStart!.Value, schedule.BreakEnd!.Value, settings);
            if (extra)
                result.Issues.Add(DayIssues.ExtraMarks);
        }

        var slots = new[] { result.EntryAm, result.ExitAm, result.EntryPm, result.ExitPm };
        if (slots.Any(t => t is { } time && (time < settings.EarliestNormalTime || time > settings.LatestNormalTime)))
            result.Issues.Add(DayIssues.OddHour);

        if (!result.IsContinuous && result.ExitAm is { } exitAm && result.EntryPm is { } entryPm
            && Minutes(entryPm - exitAm) < settings.MinBreakMinutes)
            result.Issues.Add(DayIssues.ShortBreak);

        // Atrasos.
        result.LateAmMinutes = LateMinutes(result.EntryAm, start, settings.ToleranceMinutes);
        if (!result.IsContinuous)
            result.LatePmMinutes = LateMinutes(result.EntryPm, schedule.BreakEnd!.Value, settings.ToleranceMinutes);
        if (result.ExcuseLateAm) result.LateAmMinutes = 0;
        if (result.ExcuseLatePm) result.LatePmMinutes = 0;

        // Marcas que faltan.
        if (result.IsContinuous)
        {
            CountHalf(result, result.EntryAm, result.ExitPm, wholeDay: true);
        }
        else
        {
            CountHalf(result, result.EntryAm, result.ExitAm, wholeDay: false);
            CountHalf(result, result.EntryPm, result.ExitPm, wholeDay: false);
        }

        var hasAnyMark = slots.Any(t => t is not null);
        var isComplete = result.MissingEntries == 0 && result.MissingExits == 0 && result.UnmarkedHalves == 0;

        // Horas trabajadas: solo con el día completo (con marcas que faltan el número no dice nada).
        if (isComplete && result.EntryAm is { } entry && result.ExitPm is { } exit && exit > entry)
        {
            var breakMinutes = result.IsContinuous ? 0 : Minutes(schedule.BreakEnd!.Value - schedule.BreakStart!.Value);
            var worked = Minutes(exit - entry) - breakMinutes;
            result.WorkedMinutes = worked > 0 ? worked : null;
        }

        if (result.ExcuseMissing)
        {
            result.MissingEntries = 0;
            result.MissingExits = 0;
            result.UnmarkedHalves = 0;
        }
        else if (!hasAnyMark)
        {
            result.Issues.Add(DayIssues.NoMarks);
        }
        else if (!isComplete)
        {
            result.Issues.Add(DayIssues.MissingMark);
        }

        result.Status = isComplete ? DayStatuses.Complete
            : result.ExcuseMissing ? DayStatuses.Justified
            : hasAnyMark ? DayStatuses.Incomplete
            : DayStatuses.NoMarks;

        return result;
    }

    /// <summary>"08:01 13:37 13:37 18:27" → horas ordenadas; ignora lo que no sea HH:mm.</summary>
    public static List<TimeOnly> ParseMarks(string? rawMarks)
    {
        if (string.IsNullOrWhiteSpace(rawMarks))
            return [];

        return rawMarks
            .Split([' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => TimeOnly.TryParseExact(token, ["HH:mm", "H:mm", "HH:mm:ss"], out var time) ? time : (TimeOnly?)null)
            .Where(time => time.HasValue)
            .Select(time => new TimeOnly(time!.Value.Hour, time.Value.Minute))
            .OrderBy(time => time)
            .ToList();
    }

    public static string Format(TimeOnly time) => time.ToString("HH:mm");

    /// <summary>Minutos como "HH:MM" (los atrasos acumulados pasan de 24 horas: no es una hora del día).</summary>
    public static string FormatMinutes(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

    private static List<TimeOnly> RemoveDuplicates(List<TimeOnly> marks, int windowMinutes, out bool hadDuplicates)
    {
        hadDuplicates = false;
        var kept = new List<TimeOnly>();
        foreach (var mark in marks)
        {
            if (kept.Count > 0 && Minutes(mark - kept[^1]) < windowMinutes)
            {
                hadDuplicates = true;
                continue;
            }
            kept.Add(mark);
        }
        return kept;
    }

    /// <summary>Jornada continua: primera = entrada, última = salida. Devuelve si sobraron marcas.</summary>
    private static bool AssignContinuous(DayResult result, List<TimeOnly> marks, TimeOnly start, TimeOnly end)
    {
        if (marks.Count == 0)
            return false;

        if (marks.Count == 1)
        {
            // Una sola marca: antes de la mitad de la jornada es la entrada, después la salida.
            var middle = start.AddMinutes(Minutes(end - start) / 2.0);
            if (marks[0] < middle) result.EntryAm = marks[0];
            else result.ExitPm = marks[0];
            return false;
        }

        result.EntryAm = marks[0];
        result.ExitPm = marks[^1];
        return marks.Count > 2;
    }

    /// <summary>
    /// Jornada con receso. La primera marca antes de MiddayFrom es la entrada; la última desde
    /// AfternoonExitFrom es la salida; lo que queda en medio es la salida al receso y el regreso.
    /// Devuelve si sobraron marcas.
    /// </summary>
    private static bool AssignSplit(
        DayResult result, List<TimeOnly> kept, TimeOnly breakStart, TimeOnly breakEnd, AttendanceSetting settings)
    {
        var extra = false;
        var middle = kept.Where(m => m < settings.AfternoonExitFrom).ToList();
        var evening = kept.Where(m => m >= settings.AfternoonExitFrom).ToList();

        if (middle.Count > 0 && middle[0] < settings.MiddayFrom)
        {
            result.EntryAm = middle[0];
            middle.RemoveAt(0);
        }

        result.ExitPm = evening.Count > 0 ? evening[^1] : null;
        extra |= evening.Count > 1;

        if (middle.Count >= 2)
        {
            result.ExitAm = middle[0];
            result.EntryPm = middle[1];
            extra |= middle.Count > 2;
        }
        else if (middle.Count == 1)
        {
            // Una sola marca a media mañana o hasta las 13:15 es la salida al receso: es más común
            // salir tarde a almorzar que volver con mucha anticipación.
            var mark = middle[0];
            if (mark < settings.MiddayFrom || mark < breakStart.AddMinutes(Minutes(breakEnd - breakStart) * 0.75))
                result.ExitAm = mark;
            else
                result.EntryPm = mark;
        }

        return extra;
    }

    private static int LateMinutes(TimeOnly? entry, TimeOnly expected, int toleranceMinutes)
    {
        if (entry is not { } time || time <= expected.AddMinutes(toleranceMinutes))
            return 0;
        return Minutes(time - expected);
    }

    /// <summary>Medio día (o el día entero en jornada continua): sin ninguna marca cuenta como no marcado.</summary>
    private static void CountHalf(DayResult result, TimeOnly? entry, TimeOnly? exit, bool wholeDay)
    {
        if (entry is null && exit is null)
        {
            result.UnmarkedHalves += wholeDay ? 2 : 1;
            return;
        }
        if (entry is null) result.MissingEntries++;
        if (exit is null) result.MissingExits++;
    }

    private static void ApplyAdjustmentInfo(DayResult result, AttendanceAdjustment? adjustment)
    {
        if (adjustment is null)
            return;

        result.IsAdjusted = true;
        result.OverrideMarks = adjustment.OverrideMarks;
        result.JustificationType = adjustment.JustificationType;
        result.ExcuseLateAm = adjustment.ExcuseLateAm;
        result.ExcuseLatePm = adjustment.ExcuseLatePm;
        result.ExcuseMissing = adjustment.ExcuseMissing;
        result.Reason = adjustment.Reason;
    }

    private static int Minutes(TimeSpan span) => (int)Math.Round(span.TotalMinutes);
}
