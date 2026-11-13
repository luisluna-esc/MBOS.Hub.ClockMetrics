using ClockMetrics.Core.Entities.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClockMetrics.Infrastructure.Mapping;

// Mapeos de las tablas de asistencia (Scripts/dataBase.sql, sección ClockMetrics).

public class WorkGroupConfiguration : IEntityTypeConfiguration<WorkGroup>
{
    public void Configure(EntityTypeBuilder<WorkGroup> entity)
    {
        entity.ToTable("work_group");
        entity.HasKey(e => e.WorkGroupId);
        entity.Property(e => e.WorkGroupId).HasColumnName("work_group_id").ValueGeneratedOnAdd();
        entity.Property(e => e.Name).IsRequired().HasMaxLength(50).HasColumnName("name");
        entity.Property(e => e.DisplayOrder).HasColumnName("display_order");
        entity.Property(e => e.IsActive).HasColumnName("is_active");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> entity)
    {
        entity.ToTable("employee");
        entity.HasKey(e => e.EmployeeId);
        entity.Property(e => e.EmployeeId).HasColumnName("employee_id").ValueGeneratedOnAdd();
        entity.Property(e => e.DeviceCode).IsRequired().HasMaxLength(20).HasColumnName("device_code");
        entity.Property(e => e.FullName).IsRequired().HasMaxLength(150).HasColumnName("full_name");
        entity.Property(e => e.DeviceName).IsRequired().HasMaxLength(150).HasColumnName("device_name");
        entity.Property(e => e.WorkGroupId).HasColumnName("work_group_id");
        entity.Property(e => e.IncludeInReports).HasColumnName("include_in_reports");
        entity.Property(e => e.IsActive).HasColumnName("is_active");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
    }
}

public class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
{
    public void Configure(EntityTypeBuilder<WorkSchedule> entity)
    {
        entity.ToTable("work_schedule");
        entity.HasKey(e => e.WorkScheduleId);
        entity.Property(e => e.WorkScheduleId).HasColumnName("work_schedule_id").ValueGeneratedOnAdd();
        entity.Property(e => e.DayOfWeek).HasColumnName("day_of_week");
        entity.Property(e => e.IsWorkingDay).HasColumnName("is_working_day");
        entity.Property(e => e.StartTime).HasColumnName("start_time");
        entity.Property(e => e.BreakStart).HasColumnName("break_start");
        entity.Property(e => e.BreakEnd).HasColumnName("break_end");
        entity.Property(e => e.EndTime).HasColumnName("end_time");
        entity.Ignore(e => e.IsContinuous);
    }
}

public class AttendanceSettingConfiguration : IEntityTypeConfiguration<AttendanceSetting>
{
    public void Configure(EntityTypeBuilder<AttendanceSetting> entity)
    {
        entity.ToTable("attendance_setting");
        entity.HasKey(e => e.AttendanceSettingId);
        entity.Property(e => e.AttendanceSettingId).HasColumnName("attendance_setting_id");
        entity.Property(e => e.ToleranceMinutes).HasColumnName("tolerance_minutes");
        entity.Property(e => e.DuplicateWindowMinutes).HasColumnName("duplicate_window_minutes");
        entity.Property(e => e.MinBreakMinutes).HasColumnName("min_break_minutes");
        entity.Property(e => e.MiddayFrom).HasColumnName("midday_from");
        entity.Property(e => e.AfternoonExitFrom).HasColumnName("afternoon_exit_from");
        entity.Property(e => e.EarliestNormalTime).HasColumnName("earliest_normal_time");
        entity.Property(e => e.LatestNormalTime).HasColumnName("latest_normal_time");
        entity.Property(e => e.PeriodCutoffDay).HasColumnName("period_cutoff_day");
    }
}

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> entity)
    {
        entity.ToTable("holiday");
        entity.HasKey(e => e.HolidayId);
        entity.Property(e => e.HolidayId).HasColumnName("holiday_id").ValueGeneratedOnAdd();
        entity.Property(e => e.Date).HasColumnName("holiday_date");
        entity.Property(e => e.Name).IsRequired().HasMaxLength(150).HasColumnName("name");
    }
}

public class AttendancePeriodConfiguration : IEntityTypeConfiguration<AttendancePeriod>
{
    public void Configure(EntityTypeBuilder<AttendancePeriod> entity)
    {
        entity.ToTable("attendance_period");
        entity.HasKey(e => e.AttendancePeriodId);
        entity.Property(e => e.AttendancePeriodId).HasColumnName("attendance_period_id").ValueGeneratedOnAdd();
        entity.Property(e => e.Year).HasColumnName("year");
        entity.Property(e => e.Month).HasColumnName("month");
        entity.Property(e => e.StartDate).HasColumnName("start_date");
        entity.Property(e => e.EndDate).HasColumnName("end_date");
        entity.Property(e => e.IsClosed).HasColumnName("is_closed");
        entity.Property(e => e.ClosedAt).HasColumnName("closed_at").HasColumnType("timestamp without time zone");
        entity.Property(e => e.ClosedById).HasColumnName("closed_by");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
    }
}

public class AttendanceImportConfiguration : IEntityTypeConfiguration<AttendanceImport>
{
    public void Configure(EntityTypeBuilder<AttendanceImport> entity)
    {
        entity.ToTable("attendance_import");
        entity.HasKey(e => e.AttendanceImportId);
        entity.Property(e => e.AttendanceImportId).HasColumnName("attendance_import_id").ValueGeneratedOnAdd();
        entity.Property(e => e.FileName).IsRequired().HasMaxLength(255).HasColumnName("file_name");
        entity.Property(e => e.FirstDate).HasColumnName("first_date");
        entity.Property(e => e.LastDate).HasColumnName("last_date");
        entity.Property(e => e.EmployeeCount).HasColumnName("employee_count");
        entity.Property(e => e.NewEmployeeCount).HasColumnName("new_employee_count");
        entity.Property(e => e.DayCount).HasColumnName("day_count");
        entity.Property(e => e.MarkCount).HasColumnName("mark_count");
        entity.Property(e => e.ImportedById).HasColumnName("imported_by");
        entity.Property(e => e.ImportedAt).HasColumnName("imported_at").HasColumnType("timestamp without time zone");
    }
}

public class AttendanceRawDayConfiguration : IEntityTypeConfiguration<AttendanceRawDay>
{
    public void Configure(EntityTypeBuilder<AttendanceRawDay> entity)
    {
        entity.ToTable("attendance_raw_day");
        entity.HasKey(e => e.AttendanceRawDayId);
        entity.Property(e => e.AttendanceRawDayId).HasColumnName("attendance_raw_day_id").ValueGeneratedOnAdd();
        entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
        entity.Property(e => e.WorkDate).HasColumnName("work_date");
        entity.Property(e => e.Marks).IsRequired().HasMaxLength(200).HasColumnName("marks");
        entity.Property(e => e.AttendanceImportId).HasColumnName("attendance_import_id");
    }
}

public class AttendanceAdjustmentConfiguration : IEntityTypeConfiguration<AttendanceAdjustment>
{
    public void Configure(EntityTypeBuilder<AttendanceAdjustment> entity)
    {
        entity.ToTable("attendance_adjustment");
        entity.HasKey(e => e.AttendanceAdjustmentId);
        entity.Property(e => e.AttendanceAdjustmentId).HasColumnName("attendance_adjustment_id").ValueGeneratedOnAdd();
        entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
        entity.Property(e => e.WorkDate).HasColumnName("work_date");
        entity.Property(e => e.OverrideMarks).HasColumnName("override_marks");
        entity.Property(e => e.EntryAm).HasColumnName("entry_am");
        entity.Property(e => e.ExitAm).HasColumnName("exit_am");
        entity.Property(e => e.EntryPm).HasColumnName("entry_pm");
        entity.Property(e => e.ExitPm).HasColumnName("exit_pm");
        entity.Property(e => e.JustificationType).HasMaxLength(30).HasColumnName("justification_type");
        entity.Property(e => e.ExcuseLateAm).HasColumnName("excuse_late_am");
        entity.Property(e => e.ExcuseLatePm).HasColumnName("excuse_late_pm");
        entity.Property(e => e.ExcuseMissing).HasColumnName("excuse_missing");
        entity.Property(e => e.Reason).IsRequired().HasMaxLength(500).HasColumnName("reason");
        entity.Property(e => e.CreatedById).HasColumnName("created_by");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
        entity.Property(e => e.UpdatedById).HasColumnName("updated_by");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp without time zone");
    }
}
