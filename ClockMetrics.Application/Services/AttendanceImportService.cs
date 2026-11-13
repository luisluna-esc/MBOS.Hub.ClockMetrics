using System.Net;
using ClockMetrics.Application.Attendance;
using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Application.Interfaces.Attendance;
using ClockMetrics.Application.Interfaces.Security;
using ClockMetrics.Core.Configuration;
using ClockMetrics.Core.Entities.Attendance;
using ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;
using ClockMetrics.Core.Entities.Security;
using ClockMetrics.Core.Interfaces;
using ClockMetrics.Core.QueryFilters.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ClockMetrics.Application.Services;

// Sube el archivo del biométrico. Todo o nada: con un error de lectura no se guarda ninguna fila.
//   - El empleado se reconoce por el AC-No; si es nuevo se crea (sin grupo, incluido en reportes).
//   - Las marcas de cada día se guardan tal cual; volver a subir un día las reemplaza. Los ajustes
//     de RR.HH. no se tocan.
//   - Se crean los períodos (21 al 20) que cubren las fechas del archivo.
//   - Un período cerrado no se puede volver a importar.
public class AttendanceImportService : IAttendanceImportService
{
    private const int MaxFileBytes = 10 * 1024 * 1024;

    private readonly IUnitOfWork _unitOfWork;
    private readonly AttendanceEngine _engine;
    private readonly ICurrentUserService _currentUserService;
    private readonly PaginationOptions _paginationOptions;

    public AttendanceImportService(IUnitOfWork unitOfWork, AttendanceEngine engine, ICurrentUserService currentUserService, PaginationOptions paginationOptions)
    {
        _unitOfWork = unitOfWork;
        _engine = engine;
        _currentUserService = currentUserService;
        _paginationOptions = paginationOptions;
    }

    public async Task<ResponseGetObject> Import(byte[] content, string fileName)
    {
        if (content.Length == 0)
            return Responses.GetError(HttpStatusCode.BadRequest, "El archivo está vacío.");
        if (content.Length > MaxFileBytes)
            return Responses.GetError(HttpStatusCode.BadRequest, "El archivo pasa de 10 MB.");

        var read = BiometricFileReader.Read(content);
        if (read.Errors.Count > 0)
            return new ResponseGetObject
            {
                Data = new(),
                Messages = read.Errors.Take(20).Select(e => new Core.Entities.ResponseApi.Details.Message { Type = Core.Entities.ResponseApi.Details.MessageType.Error, Description = e }).ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };

        var rows = read.Rows;
        var firstDate = rows.Min(r => r.Date);
        var lastDate = rows.Max(r => r.Date);
        var settings = await _engine.GetSettingsAsync();
        var result = new ImportResultDto { FileName = fileName, FirstDate = firstDate, LastDate = lastDate };

        // Períodos que cubre el archivo; si alguno está cerrado no se importa nada.
        var periodKeys = rows.Select(r => AttendanceEngine.PeriodOf(r.Date, settings.PeriodCutoffDay)).Distinct().ToList();
        var periodRepository = _unitOfWork.Repository<AttendancePeriod>();
        var existingPeriods = await periodRepository.Query().ToListAsync();
        var closed = existingPeriods.Where(p => p.IsClosed && periodKeys.Contains((p.Year, p.Month))).ToList();
        if (closed.Count > 0)
            return Responses.GetError(HttpStatusCode.Conflict,
                $"El período {string.Join(", ", closed.Select(p => $"{p.Month:00}/{p.Year}"))} está cerrado. Reábrelo para volver a importar.");

        // Un mismo empleado y día repetido en el archivo: se juntan las marcas.
        var byEmployeeDay = rows
            .GroupBy(r => (r.DeviceCode, r.Date))
            .ToDictionary(g => g.Key, g => string.Join(' ', AttendanceCalculator.ParseMarks(string.Join(' ', g.Select(r => r.Marks))).Select(AttendanceCalculator.Format)));
        if (byEmployeeDay.Count < rows.Count)
            result.Warnings.Add($"{rows.Count - byEmployeeDay.Count} filas repetidas (mismo AC-No y fecha): se juntaron sus marcas.");

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Empleados.
            var employeeRepository = _unitOfWork.Repository<Employee>();
            var employees = await employeeRepository.Query().ToDictionaryAsync(e => e.DeviceCode);
            foreach (var group in rows.GroupBy(r => r.DeviceCode))
            {
                var deviceName = group.Select(r => r.Name).FirstOrDefault(n => n.Length > 0) ?? group.Key;
                if (!employees.TryGetValue(group.Key, out var employee))
                {
                    employee = new Employee { DeviceCode = group.Key, FullName = deviceName, DeviceName = deviceName, CreatedAt = DateTime.UtcNow };
                    await employeeRepository.AddAsync(employee);
                    employees[group.Key] = employee;
                    result.NewEmployees.Add($"{group.Key} · {deviceName}");
                }
                else if (employee.DeviceName != deviceName)
                {
                    result.RenamedEmployees.Add($"{group.Key} · {employee.DeviceName} → {deviceName}");
                    var tracked = await employeeRepository.GetByIdAsync(employee.EmployeeId);
                    tracked!.DeviceName = deviceName;
                }
            }

            // Períodos nuevos.
            foreach (var (year, month) in periodKeys.Where(k => !existingPeriods.Any(p => p.Year == k.Year && p.Month == k.Month)))
            {
                var (start, end) = AttendanceEngine.RangeOf(year, month, settings.PeriodCutoffDay);
                await periodRepository.AddAsync(new AttendancePeriod { Year = year, Month = month, StartDate = start, EndDate = end, CreatedAt = DateTime.UtcNow });
            }

            var import = new AttendanceImport
            {
                FileName = fileName,
                FirstDate = firstDate,
                LastDate = lastDate,
                EmployeeCount = employees.Count(e => rows.Any(r => r.DeviceCode == e.Key)),
                NewEmployeeCount = result.NewEmployees.Count,
                DayCount = byEmployeeDay.Count,
                MarkCount = byEmployeeDay.Values.Sum(m => m.Length == 0 ? 0 : m.Split(' ').Length),
                ImportedById = _currentUserService.AppUserId,
                ImportedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<AttendanceImport>().AddAsync(import);
            await _unitOfWork.SaveChangesAsync();

            // Marcas: se reemplazan las de los días que trae el archivo.
            var rawRepository = _unitOfWork.Repository<AttendanceRawDay>();
            var employeeIds = employees.Values.Select(e => e.EmployeeId).ToList();
            var existingRaw = await rawRepository.Query()
                .Where(r => r.WorkDate >= firstDate && r.WorkDate <= lastDate && employeeIds.Contains(r.EmployeeId))
                .ToDictionaryAsync(r => (r.EmployeeId, r.WorkDate));

            // Días con marcas corregidas a mano: si el reloj ahora trae otra cosa, se avisa (la
            // corrección de RR.HH. sigue mandando, pero quizás ya no hace falta o hay que rehacerla).
            var overrides = await _unitOfWork.Repository<AttendanceAdjustment>().Query()
                .Where(a => a.OverrideMarks && a.WorkDate >= firstDate && a.WorkDate <= lastDate && employeeIds.Contains(a.EmployeeId))
                .ToDictionaryAsync(a => (a.EmployeeId, a.WorkDate));

            foreach (var ((deviceCode, date), marks) in byEmployeeDay)
            {
                var employeeId = employees[deviceCode].EmployeeId;
                var previousMarks = existingRaw.TryGetValue((employeeId, date), out var previous) ? previous.Marks : string.Empty;
                if (overrides.TryGetValue((employeeId, date), out var adjustment) && previousMarks != marks)
                {
                    result.AdjustedDaysChanged.Add(new ChangedAdjustedDayDto
                    {
                        EmployeeId = employeeId,
                        EmployeeName = employees[deviceCode].FullName,
                        Date = date,
                        PreviousMarks = previousMarks,
                        NewMarks = marks,
                        CorrectedMarks = CorrectedMarksText(adjustment),
                        Reason = adjustment.Reason
                    });
                }

                if (existingRaw.TryGetValue((employeeId, date), out var raw))
                {
                    raw.Marks = marks;
                    raw.AttendanceImportId = import.AttendanceImportId;
                    rawRepository.Update(raw);
                }
                else
                {
                    await rawRepository.AddAsync(new AttendanceRawDay { EmployeeId = employeeId, WorkDate = date, Marks = marks, AttendanceImportId = import.AttendanceImportId });
                }
            }
            await _unitOfWork.SaveChangesAsync();

            result.AttendanceImportId = import.AttendanceImportId;
            result.EmployeeCount = import.EmployeeCount;
            result.DayCount = import.DayCount;
            result.MarkCount = import.MarkCount;
        });

        result.AdjustedDaysChanged = result.AdjustedDaysChanged
            .OrderBy(d => d.Date)
            .ThenBy(d => d.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        result.Periods = (await periodRepository.Query().ToListAsync())
            .Where(p => periodKeys.Contains((p.Year, p.Month)))
            .OrderBy(p => p.StartDate)
            .Select(AttendanceEngine.ToDto)
            .ToList();

        return new ResponseGetObject
        {
            Data = result,
            Messages = [new Core.Entities.ResponseApi.Details.Message { Type = Core.Entities.ResponseApi.Details.MessageType.Success, Description = "Archivo importado." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static string CorrectedMarksText(AttendanceAdjustment adjustment)
    {
        var slots = new (string Label, TimeOnly? Time)[]
        {
            ("Ent AM", adjustment.EntryAm), ("Sal AM", adjustment.ExitAm), ("Ent PM", adjustment.EntryPm), ("Sal PM", adjustment.ExitPm)
        };
        return string.Join(" · ", slots.Where(s => s.Time.HasValue).Select(s => $"{s.Label} {AttendanceCalculator.Format(s.Time!.Value)}"));
    }

    public async Task<ResponseGetObject> GetImports(PaginationQueryFilter paginationQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = from i in _unitOfWork.Repository<AttendanceImport>().Query()
                    join u in _unitOfWork.Repository<AppUser>().Query() on i.ImportedById equals u.AppUserId into users
                    from u in users.DefaultIfEmpty()
                    select new AttendanceImportDto
                    {
                        AttendanceImportId = i.AttendanceImportId,
                        FileName = i.FileName,
                        FirstDate = i.FirstDate,
                        LastDate = i.LastDate,
                        EmployeeCount = i.EmployeeCount,
                        NewEmployeeCount = i.NewEmployeeCount,
                        DayCount = i.DayCount,
                        MarkCount = i.MarkCount,
                        ImportedByName = u != null ? u.FullName : null,
                        ImportedAt = i.ImportedAt
                    };

        var totalRecords = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.ImportedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Responses.Ok(new PagedResult<AttendanceImportDto> { Items = items, TotalRecords = totalRecords, PageNumber = pageNumber, PageSize = pageSize });
    }
}
