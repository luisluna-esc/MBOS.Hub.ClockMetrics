using System.Globalization;
using System.Net;
using ClockMetrics.Application.Attendance;
using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Application.Dtos.Reports;
using ClockMetrics.Application.Interfaces.Attendance;
using ClockMetrics.Application.Interfaces.Audit;
using ClockMetrics.Application.Interfaces.Security;
using ClockMetrics.Core.Constants;
using ClockMetrics.Core.Entities.Attendance;
using ClockMetrics.Core.Entities.Security;
using ClockMetrics.Core.Interfaces;
using ClockMetrics.Core.QueryFilters.Attendance;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClockMetrics.Application.Services;

// Los tres reportes que hoy se arman a mano:
//   - Registro jornada laboral (una hoja por empleado), como Informe_biometrico_mbos_<MES>.pdf.
//   - Resumen del mes (atrasos, minutos, ingresos/salidas/días no marcados), como la hoja FEBRERO.
//   - Acumulado de varios meses ordenado de más a menos atrasado, como la hoja RESUMEN.
// Todo sale de AttendanceEngine: los mismos números que la pantalla.
public class AttendanceReportService : IAttendanceReportService
{
    private const string PdfContentType = "application/pdf";
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly CultureInfo Spanish = new("es-ES");
    private static readonly string[] MonthNames = ["ENERO", "FEBRERO", "MARZO", "ABRIL", "MAYO", "JUNIO", "JULIO", "AGOSTO", "SEPTIEMBRE", "OCTUBRE", "NOVIEMBRE", "DICIEMBRE"];
    private static readonly string[] DayNames = ["Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom"];
    private static readonly string[] DayNamesUpper = ["LUNES", "MARTES", "MIÉRCOLES", "JUEVES", "VIERNES", "SÁBADO", "DOMINGO"];

    // Colores del informe manual: amarillo = falta la marca, gris = no corresponde (viernes continuo).
    private const string MissingColor = "#FFFF00";
    private const string NotApplicableColor = "#D9D9D9";
    private const string NonWorkingColor = "#EBF1DE";
    private const string HeaderColor = "#366092";
    private const string JustifiedColor = "#DDEBF7";

    // Una sola imagen para todas las páginas: QuestPDF la incrusta una vez en el PDF.
    private static readonly Lazy<Image> Logo = new(() => Image.FromBinaryData(LoadLogoBytes()));

    private readonly IUnitOfWork _unitOfWork;
    private readonly AttendanceEngine _engine;
    private readonly AttendanceService _attendanceService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public AttendanceReportService(IUnitOfWork unitOfWork, AttendanceEngine engine, AttendanceService attendanceService,
        ICurrentUserService currentUserService, IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _engine = engine;
        _attendanceService = attendanceService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    // ------------------------------------------------------------------
    // Registro jornada laboral (por empleado)
    // ------------------------------------------------------------------

    public async Task<ReportResult> EmployeeReport(long periodId, AttendanceQueryFilter filter, ReportFormat format)
    {
        var period = await _unitOfWork.Repository<AttendancePeriod>().GetByIdAsync(periodId);
        if (period is null)
            return Error(HttpStatusCode.NotFound, "No se encontró el período.");

        var employees = await _engine.CalculateAsync(period.StartDate, period.EndDate, filter.WorkGroupId, filter.EmployeeId, filter.EmployeeId is null && filter.OnlyIncludedInReports);
        if (employees.Count == 0)
            return Error(HttpStatusCode.BadRequest, "No hay empleados para ese filtro.");

        var schedules = await _unitOfWork.Repository<WorkSchedule>().Query().OrderBy(s => s.DayOfWeek).ToListAsync();
        var scheduleLines = ScheduleLines(schedules);
        var username = await CurrentUserFullNameAsync();
        var fileBase = employees.Count == 1
            ? $"registro-{Slug(employees[0].Employee.FullName)}-{period.Year}-{period.Month:00}"
            : $"registro-jornada-{period.Year}-{period.Month:00}";

        var file = format == ReportFormat.Pdf
            ? new ReportFile(BuildEmployeePdf(period, employees, scheduleLines, username), PdfContentType, $"{fileBase}.pdf")
            : new ReportFile(BuildEmployeeExcel(period, employees, scheduleLines), ExcelContentType, $"{fileBase}.xlsx");

        await _auditLogService.LogReportAccess("report_employee", "EXPORT", $"Registro jornada {MonthName(period)} ({employees.Count} empleados) en {format}");
        return new ReportResult(file, null);
    }

    private static byte[] BuildEmployeePdf(AttendancePeriod period, List<EmployeeAttendance> employees, List<(string Label, string Hours, string Break)> scheduleLines, string username)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            foreach (var employee in employees)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter.Landscape());
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(7.5f));

                    page.Header().Column(column =>
                    {
                        column.Item().AlignCenter().Height(48).Image(Logo.Value).FitHeight();
                        column.Item().PaddingTop(4).Background("#F2F2F2").AlignCenter()
                            .Text($"REGISTRO JORNADA LABORAL - {period.Year}").Bold().FontSize(11).FontColor(HeaderColor);
                        column.Item().Background("#F2F2F2").PaddingVertical(3).Row(row =>
                        {
                            row.RelativeItem(3).Column(c =>
                            {
                                foreach (var (label, hours, breakText) in scheduleLines)
                                {
                                    c.Item().Row(r =>
                                    {
                                        r.ConstantItem(90).Background(HeaderColor).PaddingHorizontal(4).Text(label).Bold().FontColor(Colors.White);
                                        r.ConstantItem(70).PaddingHorizontal(4).Text(hours).Bold().FontColor("#31869B");
                                        r.RelativeItem().PaddingHorizontal(4).Text(breakText).Bold();
                                    });
                                }
                            });
                            row.RelativeItem(2).Column(c =>
                            {
                                c.Item().Text($"Mes: {MonthName(period)}").Bold();
                                c.Item().Text($"Usuario: {employee.Employee.FullName}").Bold();
                                c.Item().Text($"Del {period.StartDate:dd/MM/yyyy} al {period.EndDate:dd/MM/yyyy}").FontSize(7);
                            });
                        });
                    });

                    page.Content().PaddingTop(4).Column(column =>
                    {
                        column.Item().Element(c => EmployeeTable(c, employee));
                        column.Item().PaddingTop(6).Element(c => EmployeeTotals(c, employee.Totals));
                        var notes = employee.Days.Where(d => d.IsAdjusted && !string.IsNullOrWhiteSpace(d.Reason)).ToList();
                        if (notes.Count > 0)
                        {
                            column.Item().PaddingTop(6).Text("Ajustes y justificaciones").Bold();
                            foreach (var day in notes)
                                column.Item().Text($"{day.Date:dd/MM}: {JustificationLabel(day.JustificationType)}{(day.OverrideMarks ? " (marcas corregidas)" : "")} — {day.Reason}").FontSize(7);
                        }
                    });

                    page.Footer().Element(c => ComposeFooter(c, username));
                });
            }
        }).GeneratePdf();
    }

    private static void EmployeeTable(IContainer container, EmployeeAttendance employee)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.2f); // empleado
                columns.RelativeColumn(1.7f); // fecha
                for (var i = 0; i < 4; i++) columns.RelativeColumn(1f);
                for (var i = 0; i < 4; i++) columns.RelativeColumn(1.3f);
                columns.RelativeColumn(2.6f); // observación
            });

            table.Header(header =>
            {
                foreach (var title in new[] { "Empleado", "Fecha", "Ent AM", "Sal AM", "Ent PM", "Sal PM", "Atrasos AM", "Atrasos PM", "Total atrasos", "Horas trabajadas", "Observación" })
                    header.Cell().Background(HeaderColor).Padding(2).AlignCenter().Text(title).Bold().FontColor(Colors.White);
            });

            foreach (var day in employee.Days)
            {
                table.Cell().Background("#F2F2F2").PaddingHorizontal(2).AlignCenter().Text(employee.Employee.FullName).FontSize(6.5f).Bold().FontColor("#595959");
                table.Cell().Background("#EBF1DE").PaddingHorizontal(2).Text($"{day.Date:dd/MM/yyyy} - {DayNames[day.DayOfWeek - 1]}").FontColor("#595959");

                if (!day.IsWorkingDay)
                {
                    var label = day.HolidayName is not null ? $"FERIADO - {day.HolidayName.ToUpper(Spanish)}" : DayNamesUpper[day.DayOfWeek - 1];
                    table.Cell().ColumnSpan(9).Background(day.HolidayName is not null ? MissingColor : NonWorkingColor)
                        .AlignCenter().Text(label).Bold().FontColor(day.HolidayName is not null ? Colors.Black : "#595959");
                    continue;
                }

                if (day.Status is DayStatuses.NoMarks or DayStatuses.Justified && day.EntryAm is null && day.ExitPm is null && day.ExitAm is null && day.EntryPm is null)
                {
                    var justified = day.Status == DayStatuses.Justified;
                    table.Cell().ColumnSpan(8).Background(justified ? JustifiedColor : MissingColor)
                        .AlignCenter().Text(justified ? JustificationLabel(day.JustificationType).ToUpper(Spanish) : "").Bold();
                    table.Cell().Element(c => ObservationCell(c, day));
                    continue;
                }

                SlotCell(table, day.EntryAm, applicable: true, day.ExcuseMissing);
                SlotCell(table, day.ExitAm, applicable: !day.IsContinuous, day.ExcuseMissing);
                SlotCell(table, day.EntryPm, applicable: !day.IsContinuous, day.ExcuseMissing);
                SlotCell(table, day.ExitPm, applicable: true, day.ExcuseMissing);
                table.Cell().Background("#F2F2F2").AlignCenter().Text(Duration(day.LateAmMinutes));
                table.Cell().Background(day.IsContinuous ? NotApplicableColor : "#F2F2F2").AlignCenter().Text(day.IsContinuous ? "" : Duration(day.LatePmMinutes));
                table.Cell().Background("#F2F2F2").AlignCenter().Text(Duration(day.LateMinutes));
                table.Cell().Background("#F2F2F2").AlignCenter().Text(day.WorkedMinutes is { } worked ? Duration(worked) : "");
                table.Cell().Element(c => ObservationCell(c, day));
            }
        });
    }

    private static void SlotCell(TableDescriptor table, TimeOnly? time, bool applicable, bool excused)
    {
        var background = !applicable ? NotApplicableColor : time is null && !excused ? MissingColor : Colors.White;
        table.Cell().Background(background).AlignCenter().Text(applicable && time is { } t ? AttendanceCalculator.Format(t) : "").Bold();
    }

    private static void ObservationCell(IContainer container, DayResult day)
    {
        var text = DayObservation(day);
        container.Background(day.IsAdjusted ? JustifiedColor : "#F2F2F2").PaddingHorizontal(2).Text(text).FontSize(6.5f);
    }

    private static void EmployeeTotals(IContainer container, AttendanceTotalsDto totals)
    {
        container.Row(row =>
        {
            row.RelativeItem(4).AlignRight().PaddingRight(10).AlignMiddle().Text("TOTAL Hrs/Min").Bold().FontSize(13);
            foreach (var minutes in new[] { totals.LateAmMinutes, totals.LatePmMinutes, totals.LateMinutes })
                row.RelativeItem(1).Background("#F2F2F2").AlignCenter().AlignMiddle().Text(Duration(minutes)).Bold().FontSize(13);
            row.RelativeItem(3).PaddingLeft(10).Column(c =>
            {
                c.Item().Text($"Atrasos (mañana y tarde): {totals.LateCount}");
                c.Item().Text($"Ingresos no marcados: {totals.MissingEntries}  ·  Salidas no marcadas: {totals.MissingExits}");
                c.Item().Text($"Días no marcados: {totals.UnmarkedDays}  ·  Medios días no marcados: {totals.UnmarkedHalfDays}");
                c.Item().Text($"Días justificados: {totals.JustifiedDays}  ·  Horas trabajadas: {Duration(totals.WorkedMinutes)}");
            });
        });
    }

    private static byte[] BuildEmployeeExcel(AttendancePeriod period, List<EmployeeAttendance> employees, List<(string Label, string Hours, string Break)> scheduleLines)
    {
        using var workbook = new XLWorkbook();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var employee in employees)
        {
            var sheet = workbook.Worksheets.Add(SheetName(employee.Employee.FullName, usedNames));
            sheet.Cell(1, 1).Value = "MISIÓN BOLIVIANA OCCIDENTAL SUR";
            sheet.Cell(2, 1).Value = $"REGISTRO JORNADA LABORAL - {period.Year}";
            sheet.Range(1, 1, 1, 11).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            sheet.Range(2, 1, 2, 11).Merge().Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml(HeaderColor)).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var line = 3;
            foreach (var (label, hours, breakText) in scheduleLines)
            {
                sheet.Cell(line, 1).Value = label;
                sheet.Cell(line, 2).Value = hours;
                sheet.Cell(line, 3).Value = breakText;
                line++;
            }
            sheet.Cell(3, 7).Value = $"Mes: {MonthName(period)}";
            sheet.Cell(4, 7).Value = $"Usuario: {employee.Employee.FullName}";

            var headerRow = Math.Max(line, 5) + 1;
            string[] titles = ["Empleado", "Fecha", "Ent AM", "Sal AM", "Ent PM", "Sal PM", "Atrasos AM", "Atrasos PM", "Total horas atrasos", "Total horas trabajadas", "Observación"];
            for (var c = 0; c < titles.Length; c++)
                sheet.Cell(headerRow, c + 1).Value = titles[c];
            StyleHeader(sheet.Range(headerRow, 1, headerRow, titles.Length));

            var row = headerRow + 1;
            foreach (var day in employee.Days)
            {
                sheet.Cell(row, 1).Value = employee.Employee.FullName;
                sheet.Cell(row, 2).Value = $"{day.Date:dd/MM/yyyy} - {DayNames[day.DayOfWeek - 1]}";

                if (!day.IsWorkingDay)
                {
                    var range = sheet.Range(row, 3, row, 10).Merge();
                    range.Value = day.HolidayName is not null ? $"FERIADO - {day.HolidayName.ToUpper(Spanish)}" : DayNamesUpper[day.DayOfWeek - 1];
                    range.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Font.SetBold()
                        .Fill.SetBackgroundColor(XLColor.FromHtml(day.HolidayName is not null ? MissingColor : NonWorkingColor));
                    row++;
                    continue;
                }

                var slots = new (TimeOnly? Time, bool Applicable)[] { (day.EntryAm, true), (day.ExitAm, !day.IsContinuous), (day.EntryPm, !day.IsContinuous), (day.ExitPm, true) };
                for (var s = 0; s < slots.Length; s++)
                {
                    var cell = sheet.Cell(row, 3 + s);
                    if (slots[s].Applicable && slots[s].Time is { } time)
                        cell.Value = AttendanceCalculator.Format(time);
                    var color = !slots[s].Applicable ? NotApplicableColor : slots[s].Time is null && !day.ExcuseMissing ? MissingColor : null;
                    if (color is not null)
                        cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml(color));
                }
                SetDuration(sheet.Cell(row, 7), day.LateAmMinutes);
                if (!day.IsContinuous) SetDuration(sheet.Cell(row, 8), day.LatePmMinutes);
                else sheet.Cell(row, 8).Style.Fill.SetBackgroundColor(XLColor.FromHtml(NotApplicableColor));
                SetDuration(sheet.Cell(row, 9), day.LateMinutes);
                if (day.WorkedMinutes is { } worked) SetDuration(sheet.Cell(row, 10), worked);
                sheet.Cell(row, 11).Value = DayObservation(day);
                if (day.IsAdjusted) sheet.Cell(row, 11).Style.Fill.SetBackgroundColor(XLColor.FromHtml(JustifiedColor));
                row++;
            }

            sheet.Cell(row + 1, 5).Value = "TOTAL Hrs/Min";
            sheet.Cell(row + 1, 5).Style.Font.SetBold();
            SetDuration(sheet.Cell(row + 1, 7), employee.Totals.LateAmMinutes);
            SetDuration(sheet.Cell(row + 1, 8), employee.Totals.LatePmMinutes);
            SetDuration(sheet.Cell(row + 1, 9), employee.Totals.LateMinutes);
            sheet.Range(row + 1, 7, row + 1, 9).Style.Font.SetBold();

            var summaryRow = row + 3;
            (string Label, int Value)[] summary =
            [
                ("Atrasos (mañana y tarde)", employee.Totals.LateCount),
                ("Ingresos no marcados", employee.Totals.MissingEntries),
                ("Salidas no marcadas", employee.Totals.MissingExits),
                ("Días no marcados", employee.Totals.UnmarkedDays),
                ("Medios días no marcados", employee.Totals.UnmarkedHalfDays),
                ("Días justificados", employee.Totals.JustifiedDays)
            ];
            foreach (var (label, value) in summary)
            {
                sheet.Cell(summaryRow, 1).Value = label;
                sheet.Cell(summaryRow, 3).Value = value;
                summaryRow++;
            }

            sheet.Columns(1, 10).AdjustToContents();
            sheet.Column(11).Width = 45;
            sheet.SheetView.FreezeRows(headerRow);
        }

        return Save(workbook);
    }

    // ------------------------------------------------------------------
    // Resumen del mes
    // ------------------------------------------------------------------

    public async Task<ReportResult> MonthSummaryReport(long periodId, AttendanceQueryFilter filter, ReportFormat format)
    {
        var period = await _unitOfWork.Repository<AttendancePeriod>().GetByIdAsync(periodId);
        if (period is null)
            return Error(HttpStatusCode.NotFound, "No se encontró el período.");

        var rows = await _engine.CalculateAsync(period.StartDate, period.EndDate, filter.WorkGroupId, filter.EmployeeId, filter.OnlyIncludedInReports);
        var username = await CurrentUserFullNameAsync();
        var fileBase = $"resumen-asistencia-{period.Year}-{period.Month:00}";

        ReportFile file;
        if (format == ReportFormat.Pdf)
        {
            file = new ReportFile(BuildSummaryPdf(period, rows, username), PdfContentType, $"{fileBase}.pdf");
        }
        else
        {
            using var workbook = new XLWorkbook();
            AddSummarySheet(workbook, MonthName(period), period, rows);
            file = new ReportFile(Save(workbook), ExcelContentType, $"{fileBase}.xlsx");
        }

        await _auditLogService.LogReportAccess("report_month_summary", "EXPORT", $"Resumen de asistencia {MonthName(period)} {period.Year} en {format}");
        return new ReportResult(file, null);
    }

    private static readonly string[] SummaryTitles =
    [
        "No.", "COD", "NOMBRE Y APELLIDOS", "GRUPO", "CANTIDAD DE ATRASOS MAÑANA Y TARDE", "MINUTOS ATRASADOS (HH:MM)",
        "INGRESOS NO MARCADOS", "SALIDAS NO MARCADAS *", "DÍAS NO MARCADOS **", "MEDIOS DÍAS NO MARCADOS", "DÍAS JUSTIFICADOS", "HORAS TRABAJADAS"
    ];

    private static readonly string[] SummaryNotes =
    [
        "*  Salidas no marcadas: como si hubieran salido antes de la hora de salida en la mañana y tarde.",
        "** Días no marcados: no marcan todo el día, como si no hubieran venido a trabajar. Medio día: no marcan toda la mañana o toda la tarde.",
        "Los atrasos y faltas justificados por RR.HH. no se cuentan."
    ];

    private static void AddSummarySheet(XLWorkbook workbook, string sheetName, AttendancePeriod period, List<EmployeeAttendance> rows)
    {
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 3).Value = "MISIÓN BOLIVIANA OCCIDENTAL SUR";
        sheet.Cell(2, 3).Value = "RESUMEN - CONTROL DE ASISTENCIA";
        sheet.Cell(3, 3).Value = $"{MonthName(period)} {period.Year}";
        sheet.Cell(4, 3).Value = $"Del: {period.StartDate:dd/MM/yyyy}";
        sheet.Cell(5, 3).Value = $"Al: {period.EndDate:dd/MM/yyyy}";
        sheet.Range(1, 3, 3, 3).Style.Font.SetBold();

        const int headerRow = 7;
        for (var c = 0; c < SummaryTitles.Length; c++)
            sheet.Cell(headerRow, c + 1).Value = SummaryTitles[c];
        StyleHeader(sheet.Range(headerRow, 1, headerRow, SummaryTitles.Length));
        sheet.Row(headerRow).Height = 45;
        sheet.Range(headerRow, 1, headerRow, SummaryTitles.Length).Style.Alignment.SetWrapText();

        var row = headerRow + 1;
        // Como el Excel manual: la numeración vuelve a 1 en cada grupo.
        foreach (var group in rows.GroupBy(r => r.WorkGroupName ?? "SIN GRUPO"))
        {
            var number = 1;
            foreach (var employee in group)
            {
                var t = employee.Totals;
                sheet.Cell(row, 1).Value = number++;
                sheet.Cell(row, 2).Value = employee.Employee.DeviceCode;
                sheet.Cell(row, 3).Value = employee.Employee.FullName;
                sheet.Cell(row, 4).Value = group.Key;
                sheet.Cell(row, 5).Value = t.LateCount;
                SetDuration(sheet.Cell(row, 6), t.LateMinutes);
                sheet.Cell(row, 7).Value = t.MissingEntries;
                sheet.Cell(row, 8).Value = t.MissingExits;
                sheet.Cell(row, 9).Value = t.UnmarkedDays;
                sheet.Cell(row, 10).Value = t.UnmarkedHalfDays;
                sheet.Cell(row, 11).Value = t.JustifiedDays;
                SetDuration(sheet.Cell(row, 12), t.WorkedMinutes);
                row++;
            }
        }

        var table = sheet.Range(headerRow, 1, Math.Max(row - 1, headerRow), SummaryTitles.Length);
        table.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        sheet.Range(headerRow + 1, 5, Math.Max(row - 1, headerRow + 1), SummaryTitles.Length).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        row += 1;
        foreach (var note in SummaryNotes)
            sheet.Cell(row++, 2).Value = note;

        sheet.Column(1).Width = 5;
        sheet.Column(2).Width = 7;
        sheet.Column(3).Width = 32;
        sheet.Column(4).Width = 10;
        sheet.Columns(5, SummaryTitles.Length).Width = 14;
        sheet.SheetView.FreezeRows(headerRow);
    }

    private static byte[] BuildSummaryPdf(AttendancePeriod period, List<EmployeeAttendance> rows, string username)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(8));
                page.Header().Element(c => ComposeHeader(c, "RESUMEN - CONTROL DE ASISTENCIA",
                    [$"{MonthName(period)} {period.Year}", $"Del {period.StartDate:dd/MM/yyyy} al {period.EndDate:dd/MM/yyyy}"]));

                page.Content().PaddingTop(6).Column(column =>
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(22);
                            columns.ConstantColumn(28);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            for (var i = 4; i < SummaryTitles.Length; i++) columns.RelativeColumn(1);
                        });
                        table.Header(header =>
                        {
                            foreach (var title in SummaryTitles)
                                header.Cell().Element(HeaderCell).AlignCenter().Text(title).FontSize(6.5f);
                        });

                        foreach (var group in rows.GroupBy(r => r.WorkGroupName ?? "SIN GRUPO"))
                        {
                            var number = 1;
                            foreach (var employee in group)
                            {
                                var t = employee.Totals;
                                table.Cell().Element(BodyCell).AlignCenter().Text($"{number++}");
                                table.Cell().Element(BodyCell).AlignCenter().Text(employee.Employee.DeviceCode);
                                table.Cell().Element(BodyCell).Text(employee.Employee.FullName);
                                table.Cell().Element(BodyCell).AlignCenter().Text(group.Key);
                                foreach (var value in new[] { $"{t.LateCount}", Duration(t.LateMinutes), $"{t.MissingEntries}", $"{t.MissingExits}", $"{t.UnmarkedDays}", $"{t.UnmarkedHalfDays}", $"{t.JustifiedDays}", Duration(t.WorkedMinutes) })
                                    table.Cell().Element(BodyCell).AlignCenter().Text(value);
                            }
                        }
                    });

                    column.Item().PaddingTop(8);
                    foreach (var note in SummaryNotes)
                        column.Item().Text(note).FontSize(7).Italic();
                });

                page.Footer().Element(c => ComposeFooter(c, username));
            });
        }).GeneratePdf();
    }

    // ------------------------------------------------------------------
    // Acumulado de varios meses
    // ------------------------------------------------------------------

    public async Task<ReportResult> AccumulatedReport(long fromPeriodId, long toPeriodId, AttendanceQueryFilter filter, ReportFormat format)
    {
        var periods = await _attendanceService.LoadPeriodRange(fromPeriodId, toPeriodId);
        if (periods is null)
            return Error(HttpStatusCode.BadRequest, "Elige un período inicial y uno final válidos.");

        var accumulated = await _attendanceService.BuildAccumulated(periods, filter);
        var username = await CurrentUserFullNameAsync();
        var title = $"CUADRO RESUMEN RETRASOS - {MonthName(periods[0])} - {MonthName(periods[^1])} {periods[^1].Year}";
        var fileBase = $"acumulado-atrasos-{periods[0].Year}-{periods[0].Month:00}-a-{periods[^1].Year}-{periods[^1].Month:00}";

        ReportFile file;
        if (format == ReportFormat.Pdf)
        {
            file = new ReportFile(BuildAccumulatedPdf(title, accumulated, username), PdfContentType, $"{fileBase}.pdf");
        }
        else
        {
            using var workbook = new XLWorkbook();
            // Una hoja por mes (como FEBRERO, MARZO, ABRIL) y al final la hoja RESUMEN.
            foreach (var period in periods)
            {
                var monthRows = await _engine.CalculateAsync(period.StartDate, period.EndDate, filter.WorkGroupId, filter.EmployeeId, filter.OnlyIncludedInReports);
                AddSummarySheet(workbook, $"{MonthName(period)} {period.Year}", period, monthRows);
            }
            AddAccumulatedSheet(workbook, title, accumulated);
            workbook.Worksheet("RESUMEN").SetTabActive().Position = 1;
            file = new ReportFile(Save(workbook), ExcelContentType, $"{fileBase}.xlsx");
        }

        await _auditLogService.LogReportAccess("report_accumulated", "EXPORT", $"{title} en {format}");
        return new ReportResult(file, null);
    }

    private static void AddAccumulatedSheet(XLWorkbook workbook, string title, AccumulatedDto accumulated)
    {
        var sheet = workbook.Worksheets.Add("RESUMEN");
        sheet.Cell(2, 2).Value = title;
        sheet.Cell(2, 2).Style.Font.SetBold().Font.SetFontSize(13);

        const int headerRow = 5;
        var titles = new List<string> { "Nro.", "Nombre", "Grupo" };
        titles.AddRange(accumulated.Periods.Select(p => $"{MonthNames[p.Month - 1]} {p.Year}"));
        titles.Add("TOTAL HORAS ACUMULADAS");
        for (var c = 0; c < titles.Count; c++)
            sheet.Cell(headerRow, c + 2).Value = titles[c];
        StyleHeader(sheet.Range(headerRow, 2, headerRow, titles.Count + 1));

        var row = headerRow + 1;
        var number = 1;
        foreach (var item in accumulated.Rows)
        {
            sheet.Cell(row, 2).Value = number++;
            sheet.Cell(row, 3).Value = item.Employee.FullName;
            sheet.Cell(row, 4).Value = item.Employee.WorkGroupName ?? "";
            for (var p = 0; p < item.LateMinutesByPeriod.Count; p++)
                SetDuration(sheet.Cell(row, 5 + p), item.LateMinutesByPeriod[p]);
            SetDuration(sheet.Cell(row, 5 + item.LateMinutesByPeriod.Count), item.Totals.LateMinutes);
            sheet.Cell(row, 5 + item.LateMinutesByPeriod.Count).Style.Font.SetBold();
            row++;
        }

        sheet.Range(headerRow, 2, Math.Max(row - 1, headerRow), titles.Count + 1).Style
            .Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        sheet.Column(2).Width = 6;
        sheet.Column(3).Width = 32;
        sheet.Column(4).Width = 10;
        sheet.Columns(5, titles.Count + 1).Width = 16;
    }

    private static byte[] BuildAccumulatedPdf(string title, AccumulatedDto accumulated, string username)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(accumulated.Periods.Count > 6 ? PageSizes.Letter.Landscape() : PageSizes.Letter);
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(8));
                page.Header().Element(c => ComposeHeader(c, title, ["De más a menos atrasado"]));

                page.Content().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(24);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        foreach (var _ in accumulated.Periods) columns.RelativeColumn(1);
                        columns.RelativeColumn(1.2f);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Nro.");
                        header.Cell().Element(HeaderCell).Text("Nombre");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Grupo");
                        foreach (var p in accumulated.Periods)
                            header.Cell().Element(HeaderCell).AlignCenter().Text($"{MonthNames[p.Month - 1]}");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("TOTAL");
                    });

                    var number = 1;
                    foreach (var item in accumulated.Rows)
                    {
                        table.Cell().Element(BodyCell).AlignCenter().Text($"{number++}");
                        table.Cell().Element(BodyCell).Text(item.Employee.FullName);
                        table.Cell().Element(BodyCell).AlignCenter().Text(item.Employee.WorkGroupName ?? "");
                        foreach (var minutes in item.LateMinutesByPeriod)
                            table.Cell().Element(BodyCell).AlignCenter().Text(Duration(minutes));
                        table.Cell().Element(BodyCell).AlignCenter().Text(Duration(item.Totals.LateMinutes)).Bold();
                    }
                });

                page.Footer().Element(c => ComposeFooter(c, username));
            });
        }).GeneratePdf();
    }

    // ------------------------------------------------------------------
    // Piezas comunes
    // ------------------------------------------------------------------

    /// <summary>"Jornada Lun - Jue | 08:30 horas | Receso 01:00", agrupando los días con el mismo horario.</summary>
    private static List<(string Label, string Hours, string Break)> ScheduleLines(List<WorkSchedule> schedules)
    {
        var lines = new List<(string, string, string)>();
        foreach (var group in schedules.Where(s => s.IsWorkingDay && s.StartTime.HasValue && s.EndTime.HasValue)
                     .GroupBy(s => (s.StartTime, s.BreakStart, s.BreakEnd, s.EndTime)))
        {
            var days = group.Select(s => s.DayOfWeek).OrderBy(d => d).ToList();
            var label = days.Count == 1 ? $"Jornada {DayNamesFull(days[0])}" : $"Jornada {DayNames[days[0] - 1]} - {DayNames[days[^1] - 1]}";
            var first = group.First();
            var breakMinutes = first.IsContinuous ? 0 : (int)(first.BreakEnd!.Value - first.BreakStart!.Value).TotalMinutes;
            var worked = (int)(first.EndTime!.Value - first.StartTime!.Value).TotalMinutes - breakMinutes;
            lines.Add((label, $"{Duration(worked)} horas", first.IsContinuous ? "Continuo" : $"Receso {Duration(breakMinutes)}"));
        }
        return lines;
    }

    private static string DayNamesFull(int dayOfWeek) => dayOfWeek switch
    {
        1 => "Lunes", 2 => "Martes", 3 => "Miércoles", 4 => "Jueves", 5 => "Viernes", 6 => "Sábado", _ => "Domingo"
    };

    private static string DayObservation(DayResult day)
    {
        var parts = new List<string>();
        if (day.IsAdjusted)
        {
            if (day.JustificationType is not null) parts.Add(JustificationLabel(day.JustificationType));
            if (day.OverrideMarks) parts.Add("Marcas corregidas");
            if (day.ExcuseLateAm || day.ExcuseLatePm) parts.Add("Atraso justificado");
        }
        else
        {
            parts.AddRange(day.Issues.Where(i => i != DayIssues.NoMarks && i != DayIssues.MissingMark).Select(IssueLabel));
        }
        return string.Join(" · ", parts);
    }

    public static string IssueLabel(string issue) => issue switch
    {
        DayIssues.Duplicate => "Marca repetida",
        DayIssues.ExtraMarks => "Marcas de más",
        DayIssues.MissingMark => "Falta marca",
        DayIssues.ShortBreak => "Receso corto",
        DayIssues.OddHour => "Hora inusual",
        DayIssues.NoMarks => "Sin marcas",
        DayIssues.MarksOnNonWorkingDay => "Marcó en día no laboral",
        _ => issue
    };

    public static string JustificationLabel(string? type) => type switch
    {
        JustificationTypes.Permission => "Permiso",
        JustificationTypes.Commission => "Comisión",
        JustificationTypes.MedicalLeave => "Baja médica",
        JustificationTypes.Vacation => "Vacación",
        JustificationTypes.ForgotToMark => "Olvido de marcar",
        JustificationTypes.DeviceFailure => "Falla del biométrico",
        JustificationTypes.Other => "Otro",
        null => "Justificado",
        _ => type
    };

    private static string MonthName(AttendancePeriod period) => MonthNames[period.Month - 1];

    private static string Duration(int minutes) => AttendanceCalculator.FormatMinutes(minutes);

    /// <summary>Duración como número de Excel con formato [h]:mm, así se puede sumar en la hoja.</summary>
    private static void SetDuration(IXLCell cell, int minutes)
    {
        cell.Value = minutes / 1440.0;
        cell.Style.NumberFormat.Format = "[h]:mm";
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.SetBold().Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderColor))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
    }

    private static string SheetName(string name, HashSet<string> used)
    {
        var clean = new string(name.Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();
        if (clean.Length == 0) clean = "Empleado";
        if (clean.Length > 28) clean = clean[..28];
        var candidate = clean;
        for (var i = 2; !used.Add(candidate); i++)
            candidate = $"{clean} {i}";
        return candidate;
    }

    private static string Slug(string value) =>
        new string(value.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void ComposeHeader(IContainer container, string title, IEnumerable<string> info)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(10);
                    c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL SUR").Bold().FontSize(10);
                    c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(10);
                });
                row.ConstantItem(140).Height(42).Image(Logo.Value).FitArea();
            });
            column.Item().PaddingTop(6).AlignCenter().Text(title).Bold().FontSize(12);
            foreach (var line in info.Where(l => !string.IsNullOrWhiteSpace(l)))
                column.Item().AlignCenter().Text(line).FontSize(9);
        });
    }

    private static void ComposeFooter(IContainer container, string username)
    {
        container.PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Text($"Generado por: {username}").FontSize(7);
            row.RelativeItem().AlignCenter().Text($"{BoliviaTime.Now:dd/MM/yyyy HH:mm}").FontSize(7);
            row.RelativeItem().AlignRight().Text(x =>
            {
                x.Span("Pág. ").FontSize(7);
                x.CurrentPageNumber().FontSize(7);
                x.Span(" de ").FontSize(7);
                x.TotalPages().FontSize(7);
            });
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Darken1).Background(HeaderColor).Padding(3).AlignMiddle()
            .DefaultTextStyle(x => x.Bold().FontColor(Colors.White));

    private static IContainer BodyCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2);

    private async Task<string> CurrentUserFullNameAsync()
    {
        if (!_currentUserService.AppUserId.HasValue)
            return "";
        var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
        return user?.FullName ?? "";
    }

    private static ReportResult Error(HttpStatusCode statusCode, string description) =>
        new(null, Responses.GetError(statusCode, description));

    private static byte[] LoadLogoBytes()
    {
        using var stream = typeof(AttendanceReportService).Assembly
            .GetManifestResourceStream("ClockMetrics.Application.Resources.mbos-logo.png")
            ?? throw new InvalidOperationException("Logo embebido (Resources/mbos-logo.png) no encontrado.");
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}
