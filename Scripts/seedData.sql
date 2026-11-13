-- =====================================================================
-- CLOCKMETRICS - DATOS INICIALES
-- Roles, usuario M-BOS, menú y visibilidad del menú por rol. Se puede volver
-- a correr: cada INSERT ignora lo que ya existe.
--
-- Sin catálogo de permisos: lo que alcanza cada rol se controla con la
-- visibilidad del menú (menu_role) y, en el backend, con
-- [Authorize(Roles = ...)] usando los grupos de RoleNames.cs.
-- =====================================================================

-- ---------------------------------------------------------------------
-- Roles. Los roles del negocio se agregan aquí y en RoleNames.cs.
-- ---------------------------------------------------------------------
INSERT INTO role (name, description, is_active) VALUES
('M-BOS', 'Superusuario: además administra usuarios, roles y menús', TRUE),
('RRHH', 'Importa el biométrico, corrige y justifica, cierra períodos', TRUE),
('Consulta', 'Solo consulta y descarga reportes', TRUE)
ON CONFLICT (name) DO NOTHING;

-- ---------------------------------------------------------------------
-- Reglas de asistencia y horario (se cambian desde Configuración).
-- Lun-Jue 08:00-12:30 / 13:30-17:30 (8:30 h), Vie 08:00-13:30 continuo (5:30 h).
-- ---------------------------------------------------------------------
INSERT INTO attendance_setting (attendance_setting_id) VALUES (1)
ON CONFLICT (attendance_setting_id) DO NOTHING;

INSERT INTO work_schedule (day_of_week, is_working_day, start_time, break_start, break_end, end_time) VALUES
(1, TRUE, '08:00', '12:30', '13:30', '17:30'),
(2, TRUE, '08:00', '12:30', '13:30', '17:30'),
(3, TRUE, '08:00', '12:30', '13:30', '17:30'),
(4, TRUE, '08:00', '12:30', '13:30', '17:30'),
(5, TRUE, '08:00', NULL, NULL, '13:30'),
(6, FALSE, NULL, NULL, NULL, NULL),
(7, FALSE, NULL, NULL, NULL, NULL)
ON CONFLICT (day_of_week) DO NOTHING;

-- Grupos de los reportes (el biométrico no los trae: se asignan a cada empleado).
INSERT INTO work_group (name, display_order) VALUES
('MBOS', 1), ('ENT', 2), ('EDUCEN', 3), ('SPA', 4)
ON CONFLICT (name) DO NOTHING;

-- Feriados nacionales 2026 de fecha fija, más el 7 de agosto que figura como feriado en el
-- informe de agosto. Los móviles (Carnaval, Viernes Santo, Corpus Christi) y los traslados por
-- decreto se cargan desde la pantalla de Feriados.
INSERT INTO holiday (holiday_date, name) VALUES
('2026-01-01', 'Año Nuevo'),
('2026-01-22', 'Día del Estado Plurinacional'),
('2026-05-01', 'Día del Trabajo'),
('2026-08-06', 'Día de la Independencia de Bolivia'),
('2026-08-07', 'Día de la Independencia de Bolivia'),
('2026-11-02', 'Día de Todos los Difuntos'),
('2026-12-25', 'Navidad')
ON CONFLICT (holiday_date) DO NOTHING;

-- ---------------------------------------------------------------------
-- Usuario M-BOS inicial. Se inicia sesión con el usuario (nombre.apellido).
-- Password en texto plano (solo para referencia, no se guarda así):
--   admin.mbos  ->  mbos12345
-- Cámbiala apenas entres en un ambiente real.
-- ---------------------------------------------------------------------
INSERT INTO app_user (full_name, username, password_hash, is_active) VALUES
('M-BOS', 'admin.mbos', '$2b$11$Ap2hxeTvzarOS3rlKveAhu7TTllP3l7Yy7Mf7GWPkAoDerQCj23ya', TRUE)
ON CONFLICT (username) DO NOTHING;

INSERT INTO app_user_role (app_user_id, role_id)
SELECT u.app_user_id, r.role_id
FROM app_user u
JOIN role r ON r.name = 'M-BOS'
WHERE u.username = 'admin.mbos'
ON CONFLICT (app_user_id, role_id) DO NOTHING;

-- ---------------------------------------------------------------------
-- Menú lateral. Agregar filas aquí a medida que se arman las pantallas;
-- parent_menu_id anida opciones. El ícono es un nombre de app-icon.
-- ---------------------------------------------------------------------
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active) VALUES
('Inicio', 'nav.home', '/', 'home', NULL, 1, TRUE)
ON CONFLICT (route) DO NOTHING;

-- Grupos (sin ruta propia).
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active)
SELECT v.name, v.translation_key, NULL, v.icon, NULL, v.display_order, TRUE
FROM (VALUES
    ('Asistencia', 'nav.attendance', 'calendar', 2),
    ('Reportes', 'nav.reports', 'report', 3),
    ('Catálogos', 'nav.catalogs', 'catalog', 4),
    ('Administración', 'nav.admin', 'settings', 99)
) AS v(name, translation_key, icon, display_order)
WHERE NOT EXISTS (SELECT 1 FROM menu m WHERE m.name = v.name AND m.parent_menu_id IS NULL);

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active)
SELECT v.name, v.translation_key, v.route, v.icon, m.menu_id, v.display_order, TRUE
FROM menu m
JOIN (VALUES
    ('Asistencia', 'Importar biométrico', 'nav.import', '/attendance/import', 'import', 1),
    ('Asistencia', 'Registro por empleado', 'nav.employeeMonth', '/attendance/employee', 'user', 2),
    ('Asistencia', 'Incoherencias', 'nav.issues', '/attendance/issues', 'alert', 3),
    ('Reportes', 'Resumen del mes', 'nav.monthSummary', '/reports/month-summary', 'report', 1),
    ('Reportes', 'Acumulado', 'nav.accumulated', '/reports/accumulated', 'chart', 2),
    ('Catálogos', 'Empleados', 'nav.employees', '/catalogs/employees', 'users', 1),
    ('Catálogos', 'Grupos', 'nav.workGroups', '/catalogs/work-groups', 'catalog', 2),
    ('Catálogos', 'Feriados', 'nav.holidays', '/catalogs/holidays', 'calendar', 3),
    ('Catálogos', 'Períodos', 'nav.periods', '/catalogs/periods', 'calendar', 4),
    ('Catálogos', 'Horario y reglas', 'nav.settings', '/catalogs/settings', 'settings', 5),
    ('Administración', 'Usuarios', 'nav.users', '/admin/users', 'users', 1),
    ('Administración', 'Roles', 'nav.roles', '/admin/roles', 'shield', 2),
    ('Administración', 'Auditoría', 'nav.auditLogs', '/admin/audit-logs', 'history', 3)
) AS v(parent_name, name, translation_key, route, icon, display_order)
  ON m.name = v.parent_name AND m.parent_menu_id IS NULL
ON CONFLICT (route) DO NOTHING;

-- ---------------------------------------------------------------------
-- menu_role: una opción se ve solo si tiene fila para el rol. Un grupo
-- también necesita la suya y se muestra si alguna opción de adentro se ve.
-- ---------------------------------------------------------------------

-- M-BOS: ve todos los menús.
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id FROM menu m, role r
WHERE r.name = 'M-BOS'
ON CONFLICT DO NOTHING;

-- RRHH: todo menos Administración, salvo la auditoría.
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m
LEFT JOIN menu p ON p.menu_id = m.parent_menu_id
CROSS JOIN role r
WHERE r.name = 'RRHH'
  AND (COALESCE(p.name, m.name) <> 'Administración'
       OR m.route = '/admin/audit-logs'
       OR (m.name = 'Administración' AND m.parent_menu_id IS NULL))
ON CONFLICT DO NOTHING;

-- Consulta: Inicio, registro por empleado, incoherencias y reportes.
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m
LEFT JOIN menu p ON p.menu_id = m.parent_menu_id
CROSS JOIN role r
WHERE r.name = 'Consulta'
  AND (m.route IN ('/', '/attendance/employee', '/attendance/issues')
       OR COALESCE(p.name, m.name) = 'Reportes'
       OR (m.name = 'Asistencia' AND m.parent_menu_id IS NULL))
ON CONFLICT DO NOTHING;
