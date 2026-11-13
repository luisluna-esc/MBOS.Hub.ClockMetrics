-- =====================================================================
-- CLOCKMETRICS - CONSOLIDATED POSTGRESQL SCRIPT
-- Base común de MBOS Hub: seguridad (usuarios, roles, menús, sesiones) y
-- auditoría. Las tablas del negocio se agregan al final, por niveles.
-- Creation order respects Foreign Key dependencies.
-- =====================================================================

-- =====================================================================
-- LEVEL 1: TABLES WITHOUT DEPENDENCIES
-- =====================================================================

CREATE TABLE role (
    role_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    description VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_role_name UNIQUE (name)
);

CREATE TABLE menu (
    menu_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    translation_key VARCHAR(100),
    route VARCHAR(150),
    icon VARCHAR(50),
    parent_menu_id BIGINT,
    display_order INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_menu_parent FOREIGN KEY (parent_menu_id) REFERENCES menu(menu_id),
    CONSTRAINT uq_menu_route UNIQUE (route)
);

CREATE INDEX idx_menu_parent ON menu(parent_menu_id);

-- =====================================================================
-- LEVEL 2: depend on level 1
-- =====================================================================

CREATE TABLE app_user (
    app_user_id BIGSERIAL PRIMARY KEY,
    ci VARCHAR(20),
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(200) NOT NULL,
    -- Con lo que se entra al sistema: nombre.apellido (minúsculas, sin tildes).
    username VARCHAR(100) NOT NULL,
    phone VARCHAR(30),
    password_changed_at TIMESTAMP,
    failed_login_attempts INT NOT NULL DEFAULT 0,
    locked_until TIMESTAMP,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    last_login TIMESTAMP,
    created_by BIGINT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_app_user_username UNIQUE (username),
    CONSTRAINT fk_app_user_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id)
);

-- =====================================================================
-- LEVEL 3: depend on app_user, menu, role
-- =====================================================================

-- Solo se guarda el SHA-256 del refresh token (64 caracteres hex): el token en claro lo tiene el cliente.
CREATE TABLE refresh_token (
    refresh_token_id BIGSERIAL PRIMARY KEY,
    app_user_id BIGINT NOT NULL,
    token_hash VARCHAR(64) NOT NULL,
    expires_at TIMESTAMP NOT NULL,
    is_revoked BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_ip VARCHAR(50),
    CONSTRAINT fk_refresh_token_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id) ON DELETE CASCADE,
    CONSTRAINT uq_refresh_token_token_hash UNIQUE (token_hash)
);

CREATE INDEX idx_refresh_token_app_user ON refresh_token(app_user_id);

CREATE TABLE password_reset (
    password_reset_id BIGSERIAL PRIMARY KEY,
    app_user_id BIGINT NOT NULL,
    token_hash VARCHAR(255) NOT NULL,
    expires_at TIMESTAMP NOT NULL,
    is_used BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_password_reset_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id) ON DELETE CASCADE,
    CONSTRAINT uq_password_reset_token_hash UNIQUE (token_hash)
);

CREATE INDEX idx_password_reset_app_user ON password_reset(app_user_id);

CREATE TABLE app_user_role (
    app_user_id BIGINT NOT NULL,
    role_id BIGINT NOT NULL,
    assigned_by BIGINT,
    assigned_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_app_user_role PRIMARY KEY (app_user_id, role_id),
    CONSTRAINT fk_app_user_role_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id) ON DELETE CASCADE,
    CONSTRAINT fk_app_user_role_role FOREIGN KEY (role_id) REFERENCES role(role_id) ON DELETE CASCADE,
    CONSTRAINT fk_app_user_role_assigned_by FOREIGN KEY (assigned_by) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_app_user_role_role ON app_user_role(role_id);

-- A sidebar menu item is visible to a role only if there is a row here for
-- that (menu, role) pair. A menu with no rows is not visible to anyone.
CREATE TABLE menu_role (
    menu_id BIGINT NOT NULL,
    role_id BIGINT NOT NULL,
    CONSTRAINT pk_menu_role PRIMARY KEY (menu_id, role_id),
    CONSTRAINT fk_menu_role_menu FOREIGN KEY (menu_id) REFERENCES menu(menu_id) ON DELETE CASCADE,
    CONSTRAINT fk_menu_role_role FOREIGN KEY (role_id) REFERENCES role(role_id) ON DELETE CASCADE
);

CREATE INDEX idx_menu_role_role ON menu_role(role_id);

-- record_id has no FK on purpose: it points to a row of whichever table is
-- named in table_name.
CREATE TABLE audit_log (
    audit_log_id BIGSERIAL PRIMARY KEY,
    app_user_id BIGINT,
    action VARCHAR(20) NOT NULL,
    module VARCHAR(100),
    table_name VARCHAR(100) NOT NULL,
    record_id BIGINT,
    description VARCHAR(500),
    old_value JSONB,
    new_value JSONB,
    ip VARCHAR(50),
    user_agent VARCHAR(500),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_audit_log_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_audit_log_app_user ON audit_log(app_user_id);
CREATE INDEX idx_audit_log_table_record ON audit_log(table_name, record_id);


-- =====================================================================
-- TABLAS DEL NEGOCIO (ClockMetrics): asistencia del biométrico.
-- =====================================================================

-- Reglas del cálculo: una sola fila (id 1). Ver AttendanceSetting.cs.
CREATE TABLE attendance_setting (
    attendance_setting_id BIGINT PRIMARY KEY,
    tolerance_minutes INT NOT NULL DEFAULT 5,
    duplicate_window_minutes INT NOT NULL DEFAULT 10,
    min_break_minutes INT NOT NULL DEFAULT 30,
    midday_from TIME NOT NULL DEFAULT '12:00',
    afternoon_exit_from TIME NOT NULL DEFAULT '16:00',
    earliest_normal_time TIME NOT NULL DEFAULT '06:00',
    latest_normal_time TIME NOT NULL DEFAULT '22:00',
    period_cutoff_day INT NOT NULL DEFAULT 20,
    CONSTRAINT ck_attendance_setting_single CHECK (attendance_setting_id = 1),
    CONSTRAINT ck_attendance_setting_cutoff CHECK (period_cutoff_day BETWEEN 1 AND 28)
);

-- Horario por día de la semana (1 = lunes ... 7 = domingo). Sin break_start/break_end es
-- jornada continua (viernes).
CREATE TABLE work_schedule (
    work_schedule_id BIGSERIAL PRIMARY KEY,
    day_of_week INT NOT NULL,
    is_working_day BOOLEAN NOT NULL DEFAULT FALSE,
    start_time TIME,
    break_start TIME,
    break_end TIME,
    end_time TIME,
    CONSTRAINT uq_work_schedule_day UNIQUE (day_of_week),
    CONSTRAINT ck_work_schedule_day CHECK (day_of_week BETWEEN 1 AND 7)
);

CREATE TABLE work_group (
    work_group_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    display_order INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_work_group_name UNIQUE (name)
);

CREATE TABLE holiday (
    holiday_id BIGSERIAL PRIMARY KEY,
    holiday_date DATE NOT NULL,
    name VARCHAR(150) NOT NULL,
    CONSTRAINT uq_holiday_date UNIQUE (holiday_date)
);

-- Mes de control: agosto 2026 = 21/07 al 20/08 (corte en period_cutoff_day).
CREATE TABLE attendance_period (
    attendance_period_id BIGSERIAL PRIMARY KEY,
    year INT NOT NULL,
    month INT NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    is_closed BOOLEAN NOT NULL DEFAULT FALSE,
    closed_at TIMESTAMP,
    closed_by BIGINT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_attendance_period UNIQUE (year, month),
    CONSTRAINT ck_attendance_period_dates CHECK (end_date >= start_date),
    CONSTRAINT fk_attendance_period_closed_by FOREIGN KEY (closed_by) REFERENCES app_user(app_user_id)
);

-- device_code es el AC-No del biométrico: con eso se reconoce a la persona al importar.
CREATE TABLE employee (
    employee_id BIGSERIAL PRIMARY KEY,
    device_code VARCHAR(20) NOT NULL,
    full_name VARCHAR(150) NOT NULL,
    device_name VARCHAR(150) NOT NULL,
    work_group_id BIGINT,
    include_in_reports BOOLEAN NOT NULL DEFAULT TRUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_employee_device_code UNIQUE (device_code),
    CONSTRAINT fk_employee_work_group FOREIGN KEY (work_group_id) REFERENCES work_group(work_group_id)
);

CREATE INDEX idx_employee_work_group ON employee(work_group_id);

CREATE TABLE attendance_import (
    attendance_import_id BIGSERIAL PRIMARY KEY,
    file_name VARCHAR(255) NOT NULL,
    first_date DATE NOT NULL,
    last_date DATE NOT NULL,
    employee_count INT NOT NULL DEFAULT 0,
    new_employee_count INT NOT NULL DEFAULT 0,
    day_count INT NOT NULL DEFAULT 0,
    mark_count INT NOT NULL DEFAULT 0,
    imported_by BIGINT,
    imported_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_attendance_import_imported_by FOREIGN KEY (imported_by) REFERENCES app_user(app_user_id)
);

-- Marcas de un día tal como vienen del biométrico. Volver a importar el mismo día las reemplaza;
-- las correcciones de RR.HH. van aparte (attendance_adjustment) y no se pierden.
CREATE TABLE attendance_raw_day (
    attendance_raw_day_id BIGSERIAL PRIMARY KEY,
    employee_id BIGINT NOT NULL,
    work_date DATE NOT NULL,
    marks VARCHAR(200) NOT NULL DEFAULT '',
    attendance_import_id BIGINT NOT NULL,
    CONSTRAINT uq_attendance_raw_day UNIQUE (employee_id, work_date),
    CONSTRAINT fk_attendance_raw_day_employee FOREIGN KEY (employee_id) REFERENCES employee(employee_id) ON DELETE CASCADE,
    CONSTRAINT fk_attendance_raw_day_import FOREIGN KEY (attendance_import_id) REFERENCES attendance_import(attendance_import_id)
);

CREATE INDEX idx_attendance_raw_day_date ON attendance_raw_day(work_date);

-- Corrección o justificación de un día (una por empleado y día), siempre con motivo.
CREATE TABLE attendance_adjustment (
    attendance_adjustment_id BIGSERIAL PRIMARY KEY,
    employee_id BIGINT NOT NULL,
    work_date DATE NOT NULL,
    override_marks BOOLEAN NOT NULL DEFAULT FALSE,
    entry_am TIME,
    exit_am TIME,
    entry_pm TIME,
    exit_pm TIME,
    justification_type VARCHAR(30),
    excuse_late_am BOOLEAN NOT NULL DEFAULT FALSE,
    excuse_late_pm BOOLEAN NOT NULL DEFAULT FALSE,
    excuse_missing BOOLEAN NOT NULL DEFAULT FALSE,
    reason VARCHAR(500) NOT NULL,
    created_by BIGINT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_by BIGINT,
    updated_at TIMESTAMP,
    CONSTRAINT uq_attendance_adjustment UNIQUE (employee_id, work_date),
    CONSTRAINT fk_attendance_adjustment_employee FOREIGN KEY (employee_id) REFERENCES employee(employee_id) ON DELETE CASCADE,
    CONSTRAINT fk_attendance_adjustment_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_attendance_adjustment_updated_by FOREIGN KEY (updated_by) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_attendance_adjustment_date ON attendance_adjustment(work_date);
