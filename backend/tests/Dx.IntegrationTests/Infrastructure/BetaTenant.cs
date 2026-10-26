using System.Globalization;
using Dx.Domain.Entities;
using Dx.Infrastructure.Security;
using Npgsql;

namespace Dx.IntegrationTests.Infrastructure;

/// <summary>
/// Second tenant with small, known data so assertions can use exact numbers (the demo
/// tenant from database/seed is loaded too, but its figures change whenever mock-data is
/// regenerated). Dates are relative to "today" in Asia/Ho_Chi_Minh, the tenant time zone.
///
/// Projects (newest first):
///   P1 Alpha Portal   RUNNING   HIGH  ends today+10   → running, risky, ending soon
///      contract staff 2 × 1.0 + 1 × 0.5 @ 4 000 → contractStaffFte 2.5, avg rate 4 000
///      assignments 1.0 (running) + 0.5 (ended yesterday) → actualStaffFte 1.0
///      approved hours 480 (+ draft / submitted ignored), budget 383 000
///      → 480 ÷ (8 × 20) = 3 FTE-months → 3 × 4 000 ÷ 383 000 × 100 = 3.13 %
///   P2 Beta CRM       PLANNING  LOW   ends today+100, budget 10 (man-months)
///      1 × 1.0 @ 5 000, approved 160 h → 1 × 5 000 ÷ (10 × 5 000) × 100 = 10 %
///   P3 Gamma_Ops      ENDING    —     ends today+200 → ending soon by status
///   P4 Delta Archive  COMPLETED LOW   ended today-5  → not ending soon
///   P5 Omega Deleted  RUNNING   soft-deleted (trash view only)
/// The member user is a member of P1 only.
/// </summary>
public static class BetaTenant
{
    public const string Password = "Beta@2026";

    public static readonly Guid TenantId = Id(1);

    public static readonly Guid AdminUserId = Id(101);
    public static readonly Guid MemberUserId = Id(102);
    public static readonly Guid GuestUserId = Id(103);
    public static readonly Guid LockUserId = Id(104);
    public static readonly Guid DisabledUserId = Id(105);
    public static readonly Guid NoTenantUserId = Id(106);

    public const string AdminEmail = "beta.admin@example.test";
    public const string MemberEmail = "beta.member@example.test";
    public const string GuestEmail = "beta.guest@example.test";
    public const string LockEmail = "beta.lock@example.test";
    public const string DisabledEmail = "beta.disabled@example.test";
    public const string NoTenantEmail = "beta.notenant@example.test";

    public static readonly Guid AdminRoleId = Id(201);
    public static readonly Guid MemberRoleId = Id(202);
    public static readonly Guid GuestRoleId = Id(203);

    public static readonly Guid StatusRunning = Id(301);
    public static readonly Guid StatusEnding = Id(302);
    public static readonly Guid StatusPlanning = Id(303);
    public static readonly Guid StatusCompleted = Id(304);
    public static readonly Guid RiskHigh = Id(311);
    public static readonly Guid RiskLow = Id(312);
    public static readonly Guid RiskInactive = Id(313);
    public static readonly Guid GradeA = Id(321);
    public static readonly Guid PositionDev = Id(331);
    public static readonly Guid EmployeeActive = Id(341);
    public static readonly Guid TaskDev = Id(351);

    public static readonly Guid ClientId = Id(401);

    public static readonly Guid EmployeeMember = Id(501);
    public static readonly Guid EmployeeAdmin = Id(502);
    public static readonly Guid EmployeeOther = Id(503);

    public static readonly Guid P1 = Id(601);
    public static readonly Guid P2 = Id(602);
    public static readonly Guid P3 = Id(603);
    public static readonly Guid P4 = Id(604);
    public static readonly Guid P5Deleted = Id(605);

    public static DateOnly Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);

    private static Guid Id(int n) => Guid.Parse($"b0000000-0000-4000-8000-{n:D12}");

    public static async Task SeedAsync(string ownerConnectionString)
    {
        var hash = new IdentityPasswordHasher().Hash(new User(), Password);
        var today = Today;
        string D(int offset) => "'" + today.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "'";
        var t = $"'{TenantId}'";
        const string empty = "{}", green = """{"color":"#22c55e"}""", red = """{"color":"#ef4444"}""";

        var sql = $"""
            INSERT INTO dx.tenants (id, code, name, time_zone) VALUES ({t}, 'test-beta', 'Beta Test Co', 'Asia/Ho_Chi_Minh');

            INSERT INTO dx.users (id, user_name, normalized_user_name, email, normalized_email, email_confirmed, password_hash, full_name, preferred_locale, is_active) VALUES
                ('{AdminUserId}', '{AdminEmail}', '{AdminEmail.ToUpperInvariant()}', '{AdminEmail}', '{AdminEmail.ToUpperInvariant()}', true, '{hash}', 'Beta Admin', 'vi', true),
                ('{MemberUserId}', '{MemberEmail}', '{MemberEmail.ToUpperInvariant()}', '{MemberEmail}', '{MemberEmail.ToUpperInvariant()}', true, '{hash}', 'Beta Member', 'en', true),
                ('{GuestUserId}', '{GuestEmail}', '{GuestEmail.ToUpperInvariant()}', '{GuestEmail}', '{GuestEmail.ToUpperInvariant()}', true, '{hash}', 'Beta Guest', NULL, true),
                ('{LockUserId}', '{LockEmail}', '{LockEmail.ToUpperInvariant()}', '{LockEmail}', '{LockEmail.ToUpperInvariant()}', true, '{hash}', 'Beta Lock', 'vi', true),
                ('{DisabledUserId}', '{DisabledEmail}', '{DisabledEmail.ToUpperInvariant()}', '{DisabledEmail}', '{DisabledEmail.ToUpperInvariant()}', true, '{hash}', 'Beta Disabled', 'vi', false),
                ('{NoTenantUserId}', '{NoTenantEmail}', '{NoTenantEmail.ToUpperInvariant()}', '{NoTenantEmail}', '{NoTenantEmail.ToUpperInvariant()}', true, '{hash}', 'Beta No Tenant', 'vi', true);

            INSERT INTO dx.tenant_users (tenant_id, user_id, is_default, is_active) VALUES
                ({t}, '{AdminUserId}', true, true),
                ({t}, '{MemberUserId}', true, true),
                ({t}, '{GuestUserId}', true, true),
                ({t}, '{LockUserId}', true, true),
                ({t}, '{DisabledUserId}', true, true),
                ({t}, '{NoTenantUserId}', true, false);

            INSERT INTO dx.roles (id, tenant_id, code, name, name_en) VALUES
                ('{AdminRoleId}', {t}, 'admin', 'Quản trị', 'Administrator'),
                ('{MemberRoleId}', {t}, 'member', 'Thành viên', 'Member'),
                ('{GuestRoleId}', {t}, 'guest', 'Khách', 'Guest');

            INSERT INTO dx.role_permissions (tenant_id, role_id, permission_id)
                SELECT {t}, '{AdminRoleId}', id FROM dx.permissions;
            INSERT INTO dx.role_permissions (tenant_id, role_id, permission_id)
                SELECT {t}, '{MemberRoleId}', id FROM dx.permissions WHERE key IN ('project.view', 'timesheet.edit');
            INSERT INTO dx.role_permissions (tenant_id, role_id, permission_id)
                SELECT {t}, '{GuestRoleId}', id FROM dx.permissions WHERE key = 'timesheet.edit';

            INSERT INTO dx.user_roles (tenant_id, user_id, role_id) VALUES
                ({t}, '{AdminUserId}', '{AdminRoleId}'),
                ({t}, '{MemberUserId}', '{MemberRoleId}'),
                ({t}, '{GuestUserId}', '{GuestRoleId}'),
                ({t}, '{LockUserId}', '{GuestRoleId}');

            INSERT INTO dx.master_data (id, tenant_id, type, code, name, name_en, metadata, sort_order, is_active) VALUES
                ('{StatusRunning}', {t}, 'project_status', 'RUNNING', 'Đang chạy', 'Running', '{green}', 1, true),
                ('{StatusEnding}', {t}, 'project_status', 'ENDING', 'Sắp kết thúc', 'Ending', '{empty}', 2, true),
                ('{StatusPlanning}', {t}, 'project_status', 'PLANNING', 'Lên kế hoạch', NULL, '{empty}', 3, true),
                ('{StatusCompleted}', {t}, 'project_status', 'COMPLETED', 'Hoàn thành', 'Completed', '{empty}', 4, true),
                ('{RiskHigh}', {t}, 'risk_level', 'HIGH', 'Cao', 'High', '{red}', 1, true),
                ('{RiskLow}', {t}, 'risk_level', 'LOW', 'Thấp', 'Low', '{empty}', 2, true),
                ('{RiskInactive}', {t}, 'risk_level', 'OLD', 'Cũ', 'Old', '{empty}', 3, false),
                ('{GradeA}', {t}, 'project_level', 'A', 'Hạng A', 'Grade A', '{empty}', 1, true),
                ('{PositionDev}', {t}, 'job_position', 'DEV', 'Lập trình viên', 'Developer', '{empty}', 1, true),
                ('{EmployeeActive}', {t}, 'employee_status', 'ACTIVE', 'Đang làm', 'Active', '{empty}', 1, true),
                ('{TaskDev}', {t}, 'task_type', 'DEV', 'Phát triển', 'Development', '{empty}', 1, true);

            INSERT INTO dx.clients (id, tenant_id, code, name) VALUES ('{ClientId}', {t}, 'BC', 'Beta Client');

            INSERT INTO dx.employees (id, tenant_id, user_id, code, full_name, email, status_id, avatar_url) VALUES
                ('{EmployeeMember}', {t}, '{MemberUserId}', 'B001', 'Beta Member', '{MemberEmail}', '{EmployeeActive}', 'https://example.test/a.png'),
                ('{EmployeeAdmin}', {t}, '{AdminUserId}', 'B002', 'Beta Admin', '{AdminEmail}', '{EmployeeActive}', NULL),
                ('{EmployeeOther}', {t}, NULL, 'B003', 'Beta Other', 'other@example.test', '{EmployeeActive}', NULL);

            INSERT INTO dx.projects (id, tenant_id, code, name, client_id, grade_id, status_id, risk_level_id, start_date, end_date, budget, currency_code, progress_percent, created_at, is_deleted) VALUES
                ('{P1}', {t}, 'B-P1', 'Alpha Portal', '{ClientId}', '{GradeA}', '{StatusRunning}', '{RiskHigh}', {D(-60)}, {D(10)}, 383000, 'USD', 40, '2026-01-04T00:00:00Z', false),
                ('{P2}', {t}, 'B-P2', 'Beta CRM', NULL, NULL, '{StatusPlanning}', '{RiskLow}', {D(5)}, {D(100)}, 10, 'USD', 0, '2026-01-03T00:00:00Z', false),
                ('{P3}', {t}, 'B-P3', 'Gamma_Ops', NULL, NULL, '{StatusEnding}', NULL, {D(-100)}, {D(200)}, NULL, NULL, 90, '2026-01-02T00:00:00Z', false),
                ('{P4}', {t}, 'B-P4', 'Delta Archive', NULL, NULL, '{StatusCompleted}', '{RiskLow}', {D(-200)}, {D(-5)}, 0, 'USD', 100, '2026-01-01T00:00:00Z', false),
                ('{P5Deleted}', {t}, 'B-P5', 'Omega Deleted', NULL, NULL, '{StatusRunning}', '{RiskHigh}', {D(-10)}, {D(3)}, NULL, NULL, 0, '2026-01-05T00:00:00Z', true);

            INSERT INTO dx.project_members (tenant_id, project_id, employee_id, is_manager) VALUES
                ({t}, '{P1}', '{EmployeeMember}', false),
                ({t}, '{P2}', '{EmployeeAdmin}', true);

            INSERT INTO dx.project_contract_staff (tenant_id, project_id, position_id, quantity, fte_per_person, start_date, end_date, monthly_rate) VALUES
                ({t}, '{P1}', '{PositionDev}', 2, 1.0, {D(-60)}, {D(10)}, 4000),
                ({t}, '{P1}', '{PositionDev}', 1, 0.5, {D(-60)}, {D(10)}, 4000),
                ({t}, '{P2}', '{PositionDev}', 1, 1.0, {D(5)}, {D(100)}, 5000);

            INSERT INTO dx.project_assignments (tenant_id, project_id, employee_id, position_id, fte_allocated, start_date, end_date) VALUES
                ({t}, '{P1}', '{EmployeeMember}', '{PositionDev}', 1.0, {D(-30)}, {D(10)}),
                ({t}, '{P1}', '{EmployeeOther}', '{PositionDev}', 0.5, {D(-60)}, {D(-1)}),
                ({t}, '{P2}', '{EmployeeAdmin}', '{PositionDev}', 0.75, {D(5)}, {D(100)});

            -- P1: 60 × 8 h approved = 480 h; P2: 20 × 8 h approved = 160 h; draft / submitted rows are ignored.
            INSERT INTO dx.timesheet_entries (tenant_id, employee_id, project_id, task_type_id, work_date, hours, status)
                SELECT {t}, '{EmployeeMember}', '{P1}', '{TaskDev}', {D(0)}::date - g, 8, 'approved' FROM generate_series(1, 60) g;
            INSERT INTO dx.timesheet_entries (tenant_id, employee_id, project_id, task_type_id, work_date, hours, status)
                SELECT {t}, '{EmployeeAdmin}', '{P2}', '{TaskDev}', {D(0)}::date - g, 8, 'approved' FROM generate_series(1, 20) g;
            INSERT INTO dx.timesheet_entries (tenant_id, employee_id, project_id, task_type_id, work_date, hours, status) VALUES
                ({t}, '{EmployeeMember}', '{P1}', '{TaskDev}', {D(0)}, 8, 'draft'),
                ({t}, '{EmployeeMember}', '{P1}', '{TaskDev}', {D(0)}, 8, 'submitted');

            INSERT INTO dx.tenant_settings (tenant_id, key, value) VALUES
                ({t}, 'company_name', 'Beta Test Co'),
                ({t}, 'currency', 'VND'),
                ({t}, 'work_week', 'MON-SAT');

            INSERT INTO dx.tenant_rules (tenant_id, rule_key, value, is_enabled) VALUES
                ({t}, 'PROJECT_ENDING_SOON_DAYS', '30', true),
                ({t}, 'TIMESHEET_FTE_HOURS', '8', true),
                ({t}, 'TIMESHEET_MAX_HOURS', '10', false);
            """;

        await using var conn = new NpgsqlConnection(ownerConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await using (var cmd = new NpgsqlCommand(sql, conn, tx)) await cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }
}
