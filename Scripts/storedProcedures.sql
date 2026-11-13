-- =====================================================================
-- CLOCKMETRICS - FUNCIONES (stored procedures)
-- Se pueden volver a correr: cada una hace DROP + CREATE OR REPLACE.
-- =====================================================================

-- ---------------------------------------------------------------------
-- Auditoría: quién creó, editó o eliminó qué y cuándo. Solo esas tres acciones (no las descargas
-- de reportes), sin las sesiones (refresh_token) ni los ingresos al sistema (app_user que solo
-- cambia la fecha de ingreso o los intentos fallidos). record_label dice de qué registro se trata
-- para no mostrar solo un id: al agregar tablas del negocio, sumar su WHEN aquí.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_get_audit_logs;
CREATE OR REPLACE FUNCTION sp_get_audit_logs(
    p_app_user_id BIGINT,
    p_table_name VARCHAR,
    p_record_id BIGINT,
    p_action VARCHAR,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    audit_log_id BIGINT,
    app_user_id BIGINT,
    app_user_full_name VARCHAR,
    action VARCHAR,
    table_name VARCHAR,
    record_id BIGINT,
    record_label TEXT,
    old_value TEXT,
    new_value TEXT,
    created_at TIMESTAMPTZ,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    WITH logs AS (
        SELECT
            al.*,
            COALESCE(al.new_value, al.old_value) AS v,
            CAST(COUNT(*) OVER() AS INT) AS total_records
        FROM audit_log al
        WHERE al.action IN ('INSERT', 'UPDATE', 'DELETE')
          AND al.table_name <> 'refresh_token'
          AND NOT (
              al.table_name = 'app_user' AND al.action = 'UPDATE'
              AND (al.old_value - ARRAY['LastLogin', 'FailedLoginAttempts', 'LockedUntil'])
                = (al.new_value - ARRAY['LastLogin', 'FailedLoginAttempts', 'LockedUntil'])
          )
          AND (p_app_user_id IS NULL OR al.app_user_id = p_app_user_id)
          AND (p_table_name IS NULL OR al.table_name = p_table_name)
          AND (p_record_id IS NULL OR al.record_id = p_record_id)
          AND (p_action IS NULL OR al.action = p_action)
        ORDER BY al.created_at DESC, al.audit_log_id DESC
        LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size
    )
    SELECT
        l.audit_log_id,
        l.app_user_id,
        u.full_name AS app_user_full_name,
        l.action,
        l.table_name,
        l.record_id,
        CASE l.table_name
            WHEN 'attendance_adjustment' THEN concat_ws(' · ',
                (SELECT e.full_name FROM employee e WHERE e.employee_id = (l.v->>'EmployeeId')::bigint),
                to_char((l.v->>'WorkDate')::date, 'DD/MM/YYYY'))
            WHEN 'attendance_period' THEN lpad(l.v->>'Month', 2, '0') || '/' || (l.v->>'Year')
            WHEN 'attendance_import' THEN l.v->>'FileName'
            WHEN 'holiday' THEN concat_ws(' ', to_char((l.v->>'Date')::date, 'DD/MM/YYYY'), l.v->>'Name')
            WHEN 'work_schedule' THEN 'Día ' || (l.v->>'DayOfWeek')
            WHEN 'attendance_setting' THEN 'Reglas de asistencia'
            WHEN 'app_user_role' THEN concat_ws(' · ',
                (SELECT au.full_name FROM app_user au WHERE au.app_user_id = (l.v->>'AppUserId')::bigint),
                (SELECT r.name FROM role r WHERE r.role_id = (l.v->>'RoleId')::bigint))
            WHEN 'menu_role' THEN concat_ws(' · ',
                (SELECT r.name FROM role r WHERE r.role_id = (l.v->>'RoleId')::bigint),
                (SELECT m.name FROM menu m WHERE m.menu_id = (l.v->>'MenuId')::bigint))
            ELSE COALESCE(l.v->>'FullName', l.v->>'Name', l.v->>'Username')
        END AS record_label,
        l.old_value::TEXT,
        l.new_value::TEXT,
        l.created_at AT TIME ZONE 'UTC' AS created_at,
        l.total_records
    FROM logs l
    LEFT JOIN app_user u ON u.app_user_id = l.app_user_id
    ORDER BY l.created_at DESC, l.audit_log_id DESC;
$$;

-- ---------------------------------------------------------------------
-- Purga de auditoría: borra lo que tiene más de p_retention_years años. La llama
-- AuditRetentionBackgroundService una vez cada AuditRetention:IntervalHours.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_purge_audit_logs;
CREATE OR REPLACE FUNCTION sp_purge_audit_logs(p_retention_years INT)
RETURNS TABLE (deleted_rows INT)
LANGUAGE plpgsql
AS $$
DECLARE
    v_deleted INT;
BEGIN
    DELETE FROM audit_log
    WHERE created_at < (NOW() AT TIME ZONE 'UTC') - make_interval(years => GREATEST(p_retention_years, 1));
    GET DIAGNOSTICS v_deleted = ROW_COUNT;
    RETURN QUERY SELECT v_deleted;
END;
$$;
