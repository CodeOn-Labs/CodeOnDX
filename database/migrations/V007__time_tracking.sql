-- =============================================================================
-- V007 - Time tracking (D4): timesheet entries, leave and overtime requests,
-- leave balances, attendance (check-in / check-out)
-- =============================================================================

-- -----------------------------------------------------------------------------
-- timesheet_entries: daily work log lines.
--   source = manual          -> typed by the employee
--   source = leave_request   -> generated when a leave request is approved
--   source = overtime_request-> generated when an OT request is approved
-- Generated rows are deleted when the request is rejected / cancelled.
-- Limits (min/max hours, edit window, lock day) come from tenant_rules TIMESHEET_*.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.timesheet_entries (
    id                 uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id          uuid          NOT NULL,
    employee_id        uuid          NOT NULL,
    project_id         uuid,
    task_type_id       uuid          NOT NULL,
    work_date          date          NOT NULL,
    hours              numeric(5,2)  NOT NULL,
    description        varchar(2000),
    status             varchar(20)   NOT NULL DEFAULT 'draft',
    source             varchar(20)   NOT NULL DEFAULT 'manual',
    source_request_id  uuid,
    approved_by        uuid,
    approved_at        timestamptz,
    rejection_reason   varchar(1000),
    created_at         timestamptz   NOT NULL DEFAULT now(),
    created_by         uuid,
    updated_at         timestamptz   NOT NULL DEFAULT now(),
    updated_by         uuid,
    CONSTRAINT pk_timesheet_entries PRIMARY KEY (id),
    CONSTRAINT fk_timesheet_entries_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_timesheet_entries_employee_id FOREIGN KEY (employee_id) REFERENCES dx.employees (id),
    CONSTRAINT fk_timesheet_entries_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id),
    CONSTRAINT fk_timesheet_entries_task_type_id FOREIGN KEY (task_type_id) REFERENCES dx.master_data (id),
    CONSTRAINT fk_timesheet_entries_approved_by FOREIGN KEY (approved_by) REFERENCES dx.users (id),
    CONSTRAINT ck_timesheet_entries_hours CHECK (hours > 0 AND hours <= 24),
    CONSTRAINT ck_timesheet_entries_status CHECK (status IN ('draft', 'submitted', 'approved', 'rejected')),
    CONSTRAINT ck_timesheet_entries_source CHECK (source IN ('manual', 'leave_request', 'overtime_request')),
    CONSTRAINT ck_timesheet_entries_source_request CHECK ((source = 'manual') = (source_request_id IS NULL))
);
CREATE INDEX ix_timesheet_entries_tenant_id_employee_id_work_date ON dx.timesheet_entries (tenant_id, employee_id, work_date);
CREATE INDEX ix_timesheet_entries_tenant_id_project_id_work_date ON dx.timesheet_entries (tenant_id, project_id, work_date) WHERE project_id IS NOT NULL;
CREATE INDEX ix_timesheet_entries_source_request_id ON dx.timesheet_entries (source_request_id) WHERE source_request_id IS NOT NULL;
CREATE INDEX ix_timesheet_entries_task_type_id ON dx.timesheet_entries (task_type_id);

-- -----------------------------------------------------------------------------
-- leave_requests. leave_type_id -> master_data(leave_type) whose metadata says
-- paid / deduct_balance / need_attachment. Code NP-0001 style, per tenant.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.leave_requests (
    id              uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id       uuid          NOT NULL,
    code            varchar(20)   NOT NULL,
    employee_id     uuid          NOT NULL,
    leave_type_id   uuid          NOT NULL,
    from_date       date          NOT NULL,
    to_date         date          NOT NULL,
    duration_type   varchar(20)   NOT NULL DEFAULT 'full_day',
    total_hours     numeric(6,2)  NOT NULL,
    reason          varchar(2000),
    attachment_url  varchar(1000),
    status          varchar(20)   NOT NULL DEFAULT 'pending',
    decided_by      uuid,
    decided_at      timestamptz,
    decision_note   varchar(1000),
    created_at      timestamptz   NOT NULL DEFAULT now(),
    created_by      uuid,
    updated_at      timestamptz   NOT NULL DEFAULT now(),
    updated_by      uuid,
    CONSTRAINT pk_leave_requests PRIMARY KEY (id),
    CONSTRAINT uq_leave_requests_tenant_id_code UNIQUE (tenant_id, code),
    CONSTRAINT fk_leave_requests_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_leave_requests_employee_id FOREIGN KEY (employee_id) REFERENCES dx.employees (id),
    CONSTRAINT fk_leave_requests_leave_type_id FOREIGN KEY (leave_type_id) REFERENCES dx.master_data (id),
    CONSTRAINT fk_leave_requests_decided_by FOREIGN KEY (decided_by) REFERENCES dx.users (id),
    CONSTRAINT ck_leave_requests_dates CHECK (to_date >= from_date),
    CONSTRAINT ck_leave_requests_duration_type CHECK (duration_type IN ('full_day', 'half_day_am', 'half_day_pm')),
    CONSTRAINT ck_leave_requests_half_day_single_date CHECK (duration_type = 'full_day' OR from_date = to_date),
    CONSTRAINT ck_leave_requests_total_hours CHECK (total_hours > 0),
    CONSTRAINT ck_leave_requests_status CHECK (status IN ('pending', 'approved', 'rejected', 'cancelled')),
    CONSTRAINT ck_leave_requests_rejection_note CHECK (status <> 'rejected' OR decision_note IS NOT NULL)
);
CREATE INDEX ix_leave_requests_tenant_id_employee_id_from_date ON dx.leave_requests (tenant_id, employee_id, from_date);
CREATE INDEX ix_leave_requests_tenant_id_status ON dx.leave_requests (tenant_id, status) WHERE status = 'pending';
CREATE INDEX ix_leave_requests_leave_type_id ON dx.leave_requests (leave_type_id);

-- -----------------------------------------------------------------------------
-- overtime_requests. rate_percent is copied at creation (150 weekday /
-- 200 weekend / 300 holiday by default). Limits: tenant_rules OT_MAX_HOURS_*.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.overtime_requests (
    id             uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id      uuid          NOT NULL,
    code           varchar(20)   NOT NULL,
    employee_id    uuid          NOT NULL,
    project_id     uuid,
    work_date      date          NOT NULL,
    start_time     time          NOT NULL,
    end_time       time          NOT NULL,
    hours          numeric(5,2)  NOT NULL,
    day_type       varchar(10)   NOT NULL,
    rate_percent   smallint      NOT NULL,
    reason         varchar(2000),
    status         varchar(20)   NOT NULL DEFAULT 'pending',
    decided_by     uuid,
    decided_at     timestamptz,
    decision_note  varchar(1000),
    created_at     timestamptz   NOT NULL DEFAULT now(),
    created_by     uuid,
    updated_at     timestamptz   NOT NULL DEFAULT now(),
    updated_by     uuid,
    CONSTRAINT pk_overtime_requests PRIMARY KEY (id),
    CONSTRAINT uq_overtime_requests_tenant_id_code UNIQUE (tenant_id, code),
    CONSTRAINT fk_overtime_requests_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_overtime_requests_employee_id FOREIGN KEY (employee_id) REFERENCES dx.employees (id),
    CONSTRAINT fk_overtime_requests_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id),
    CONSTRAINT fk_overtime_requests_decided_by FOREIGN KEY (decided_by) REFERENCES dx.users (id),
    CONSTRAINT ck_overtime_requests_times CHECK (end_time > start_time),
    CONSTRAINT ck_overtime_requests_hours CHECK (hours > 0 AND hours <= 24),
    CONSTRAINT ck_overtime_requests_day_type CHECK (day_type IN ('weekday', 'weekend', 'holiday')),
    CONSTRAINT ck_overtime_requests_rate_percent CHECK (rate_percent BETWEEN 100 AND 500),
    CONSTRAINT ck_overtime_requests_status CHECK (status IN ('pending', 'approved', 'rejected', 'cancelled')),
    CONSTRAINT ck_overtime_requests_rejection_note CHECK (status <> 'rejected' OR decision_note IS NOT NULL)
);
CREATE INDEX ix_overtime_requests_tenant_id_employee_id_work_date ON dx.overtime_requests (tenant_id, employee_id, work_date);
CREATE INDEX ix_overtime_requests_tenant_id_status ON dx.overtime_requests (tenant_id, status) WHERE status = 'pending';
CREATE INDEX ix_overtime_requests_project_id ON dx.overtime_requests (project_id);

-- -----------------------------------------------------------------------------
-- leave_balances: yearly annual-leave entitlement per employee.
-- available = entitled + carried_over + adjusted - used (used computed from
-- approved leave requests with deduct_balance = true).
-- -----------------------------------------------------------------------------
CREATE TABLE dx.leave_balances (
    id                 uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id          uuid          NOT NULL,
    employee_id        uuid          NOT NULL,
    year               smallint      NOT NULL,
    entitled_days      numeric(5,2)  NOT NULL DEFAULT 0,
    carried_over_days  numeric(5,2)  NOT NULL DEFAULT 0,
    adjusted_days      numeric(5,2)  NOT NULL DEFAULT 0,
    note               varchar(1000),
    created_at         timestamptz   NOT NULL DEFAULT now(),
    created_by         uuid,
    updated_at         timestamptz   NOT NULL DEFAULT now(),
    updated_by         uuid,
    CONSTRAINT pk_leave_balances PRIMARY KEY (id),
    CONSTRAINT uq_leave_balances_tenant_id_employee_id_year UNIQUE (tenant_id, employee_id, year),
    CONSTRAINT fk_leave_balances_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_leave_balances_employee_id FOREIGN KEY (employee_id) REFERENCES dx.employees (id),
    CONSTRAINT ck_leave_balances_year CHECK (year BETWEEN 2000 AND 2100),
    CONSTRAINT ck_leave_balances_days CHECK (entitled_days >= 0 AND carried_over_days >= 0)
);

-- -----------------------------------------------------------------------------
-- attendance_records: one row per employee per day. Times are local wall-clock
-- time in the tenant time zone (tenants.time_zone).
--   source = app    -> Check-in / Check-out button (server time)
--   source = import -> file exported from a time clock
--   source = manual -> edited by a manager (note required)
-- -----------------------------------------------------------------------------
CREATE TABLE dx.attendance_records (
    id             uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id      uuid          NOT NULL,
    employee_id    uuid          NOT NULL,
    work_date      date          NOT NULL,
    check_in       time,
    check_out      time,
    source         varchar(10)   NOT NULL DEFAULT 'app',
    note           varchar(1000),
    created_at     timestamptz   NOT NULL DEFAULT now(),
    created_by     uuid,
    updated_at     timestamptz   NOT NULL DEFAULT now(),
    updated_by     uuid,
    CONSTRAINT pk_attendance_records PRIMARY KEY (id),
    CONSTRAINT uq_attendance_records_tenant_id_employee_id_work_date UNIQUE (tenant_id, employee_id, work_date),
    CONSTRAINT fk_attendance_records_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_attendance_records_employee_id FOREIGN KEY (employee_id) REFERENCES dx.employees (id),
    CONSTRAINT ck_attendance_records_source CHECK (source IN ('app', 'import', 'manual')),
    CONSTRAINT ck_attendance_records_times CHECK (check_out IS NULL OR check_in IS NULL OR check_out >= check_in),
    CONSTRAINT ck_attendance_records_manual_note CHECK (source <> 'manual' OR note IS NOT NULL)
);
CREATE INDEX ix_attendance_records_tenant_id_work_date ON dx.attendance_records (tenant_id, work_date);
