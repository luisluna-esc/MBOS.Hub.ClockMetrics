using ClockMetrics.Application.Attendance;
using ClockMetrics.Application.Dtos.Attendance;
using ClockMetrics.Application.Dtos.Security;
using ClockMetrics.Application.Interfaces.Attendance;
using ClockMetrics.Application.Interfaces.Audit;
using ClockMetrics.Application.Interfaces.Security;
using ClockMetrics.Application.Services;
using ClockMetrics.Application.Validators.Attendance;
using ClockMetrics.Application.Validators.Security;
using ClockMetrics.Core.Configuration;
using ClockMetrics.Core.Interfaces;
using ClockMetrics.Infrastructure.Context;
using ClockMetrics.Infrastructure.Context.Core;
using ClockMetrics.Infrastructure.Interceptors;
using ClockMetrics.Infrastructure.Security;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ClockMetrics.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Missing connection string 'DefaultConnection' in appsettings.");

        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options.UseNpgsql(connectionString)
                   .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.Configure<PaginationOptions>(configuration.GetSection("Pagination"));
        services.AddScoped(sp => sp.GetRequiredService<IOptions<PaginationOptions>>().Value);

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddScoped(sp => sp.GetRequiredService<IOptions<JwtOptions>>().Value);

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IValidator<RoleDto>, RoleValidator>();

        services.AddScoped<IAppUserService, AppUserService>();
        services.AddScoped<IValidator<AppUserDto>, AppUserValidator>();

        services.AddScoped<IAppUserRoleService, AppUserRoleService>();

        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IValidator<MenuDto>, MenuValidator>();
        services.AddScoped<IRoleMenuService, RoleMenuService>();

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();

        services.Configure<AuditRetentionOptions>(configuration.GetSection("AuditRetention"));
        services.AddScoped(sp => sp.GetRequiredService<IOptions<AuditRetentionOptions>>().Value);
        services.AddScoped<IAuditLogService, AuditLogService>();

        // Asistencia del biométrico.
        services.AddScoped<AttendanceEngine>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<IAttendanceService>(sp => sp.GetRequiredService<AttendanceService>());
        services.AddScoped<IValidator<AdjustmentRequestDto>, AdjustmentRequestValidator>();
        services.AddScoped<IAttendanceImportService, AttendanceImportService>();
        services.AddScoped<IAttendanceReportService, AttendanceReportService>();

        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IValidator<EmployeeRequestDto>, EmployeeRequestValidator>();
        services.AddScoped<IWorkGroupService, WorkGroupService>();
        services.AddScoped<IValidator<WorkGroupDto>, WorkGroupValidator>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<IValidator<HolidayDto>, HolidayValidator>();
        services.AddScoped<IAttendancePeriodService, AttendancePeriodService>();
        services.AddScoped<IValidator<PeriodRequestDto>, PeriodRequestValidator>();
        services.AddScoped<IAttendanceSettingService, AttendanceSettingService>();
        services.AddScoped<IValidator<AttendanceSettingsDto>, AttendanceSettingsValidator>();
        return services;
    }
}
