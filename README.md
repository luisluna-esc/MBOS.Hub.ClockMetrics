# ClockMetrics (backend)

API de ClockMetrics en .NET 10 + PostgreSQL. Sale de la base común de FundTrack: seguridad
(usuarios, roles, menú por rol, JWT con refresh token), auditoría y formato de respuestas. El
frontend está en `MBOS.Hub.ClockMetrics.Web`.

| Proyecto | Qué tiene |
|---|---|
| `ClockMetrics.Core` | Entidades, constantes (`RoleNames`), `QueryFilters`, respuestas (`ResponseGetObject`, `ResponsePost`, `PagedResult`), `IUnitOfWork` |
| `ClockMetrics.Application` | DTOs, interfaces, servicios y validadores (FluentValidation) |
| `ClockMetrics.Infrastructure` | `AppDbContext`, mapeos EF (snake_case), auditoría automática, DI |
| `ClockMetrics.Web` | Controladores, `Program.cs`, purga periódica de la auditoría |

## Primera vez en desarrollo

1. Crear la base y correr los scripts, en este orden:

   ```bash
   psql -U postgres -c 'CREATE DATABASE "ClockMetrics"'
   psql -U postgres -d ClockMetrics -v ON_ERROR_STOP=1 -f Scripts/dataBase.sql
   psql -U postgres -d ClockMetrics -v ON_ERROR_STOP=1 -f Scripts/storedProcedures.sql
   psql -U postgres -d ClockMetrics -v ON_ERROR_STOP=1 -f Scripts/seedData.sql
   ```

2. Los secretos no van en `appsettings.json`. En desarrollo se guardan con user-secrets:

   ```bash
   cd ClockMetrics.Web
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=ClockMetrics;Username=postgres;Password=<clave>"
   dotnet user-secrets set "Jwt:Key" "$(node -e "console.log(require('crypto').randomBytes(64).toString('base64'))")"
   ```

   En el servidor van como variables de entorno: `ConnectionStrings__DefaultConnection`,
   `Jwt__Key` y `Cors__AllowedOrigins__0`.

3. `dotnet run` dentro de `ClockMetrics.Web` (http://localhost:5152, Swagger en `/swagger`).
   Se entra con `admin.mbos` / `mbos12345`: cámbiala en cualquier ambiente real.

## Cómo funciona la asistencia

1. **Importar** (`POST /api/attendance/imports`): el archivo que exporta el reloj (`.xls` BIFF2, que
   ninguna librería abre; lo lee `BiometricFileReader`) o un `.xlsx` con las columnas AC-No, Nombre,
   Dpto., Fecha y Hora. El empleado se reconoce por el AC-No; los nuevos se crean solos. Las marcas
   se guardan tal cual en `attendance_raw_day` y se crean los períodos (21 al 20) que cubre el archivo.
2. **Calcular** (`AttendanceCalculator`, función pura): quita las marcas repetidas, pone cada marca
   en su casilla (Ent AM, Sal AM, Ent PM, Sal PM), calcula atrasos (pasada la tolerancia, desde la
   hora de entrada) y horas trabajadas, y marca las incoherencias. Nada se guarda calculado: un cambio
   de regla o un ajuste se ve al instante.
3. **Ajustar** (`attendance_adjustment`): RR.HH. corrige las marcas de un día o justifica atrasos y
   faltas, siempre con motivo. El dato del biométrico no se toca y todo queda en la auditoría.
4. **Reportes** (`AttendanceReportService`, PDF con QuestPDF y Excel con ClosedXML): registro jornada
   laboral por empleado, resumen del mes y acumulado de varios meses.

Contra el informe manual de agosto 2026, el cálculo coincide en el 95 % de los días; las diferencias
son correcciones hechas a mano en ese PDF, que aquí aparecen como incoherencias para justificar.

## Agregar una pantalla del negocio

1. Tabla en `Scripts/dataBase.sql` (y una `migration_XX_*.sql` si la base ya existe).
2. Entidad en `Core/Entities`, mapeo en `Infrastructure/Mapping`, `DbSet` en `AppDbContext`.
3. DTO, interfaz, servicio y validador en `Application` (el modelo es `RoleService`), registrados en
   `DependencyInjection.cs`.
4. Controlador en `Web/Controllers`, con `[Authorize(Roles = RoleNames.Writers)]` en lo que escribe.
5. Fila en `menu` y `menu_role` (`Scripts/seedData.sql`), y un `WHEN` en `sp_get_audit_logs` para que
   la auditoría muestre el nombre del registro.

## Diferencias con FundTrack

- El refresh token se guarda como SHA-256 (`refresh_token.token_hash`), no en claro.
- CORS acepta `localhost` solo en desarrollo; en producción, solo `Cors:AllowedOrigins`.
- Los enums viajan como texto (`"Success"`, `"Error"`).
- `sp_purge_audit_logs` está en `Scripts/storedProcedures.sql` (en FundTrack falta en los scripts).
- `Microsoft.AspNetCore.Http` 2.3.x se cambió por `FrameworkReference` (el paquete viejo traía una
  vulnerabilidad conocida).
