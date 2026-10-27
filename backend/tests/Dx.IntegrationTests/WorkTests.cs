using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.IntegrationTests.Infrastructure;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>
/// Work log, leave / OT requests, attendance and the effort grid on the beta tenant. Lookups the
/// beta seed does not have (task types PROJECT / LEAVE / OT / MEETING, leave type ANNUAL…) are
/// added per test and removed afterwards.
/// </summary>
public sealed class WorkTests(ApiFixture fx)
{
    private static readonly Guid TaskProject = Guid.Parse("b0000000-0000-4000-8000-000000000901");
    private static readonly Guid TaskLeave = Guid.Parse("b0000000-0000-4000-8000-000000000902");
    private static readonly Guid TaskOt = Guid.Parse("b0000000-0000-4000-8000-000000000903");
    private static readonly Guid TaskMeeting = Guid.Parse("b0000000-0000-4000-8000-000000000904");
    private static readonly Guid LeaveAnnual = Guid.Parse("b0000000-0000-4000-8000-000000000905");
    private static readonly Guid Department = Guid.Parse("b0000000-0000-4000-8000-000000000906");
    private static readonly Guid Team = Guid.Parse("b0000000-0000-4000-8000-000000000907");
    private static readonly Guid LevelFull = Guid.Parse("b0000000-0000-4000-8000-000000000908");

    private static string D(int offset) => Iso(BetaTenant.Today.AddDays(offset));

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>First Monday–Friday day on or after today + offset.</summary>
    private static DateOnly Weekday(int offset)
    {
        var d = BetaTenant.Today.AddDays(offset);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
        return d;
    }

    private async Task SeedLookupsAsync()
    {
        var t = $"'{BetaTenant.TenantId}'";
        await fx.ExecuteOwnerAsync($$"""
            INSERT INTO dx.master_data (id, tenant_id, type, code, name, metadata, sort_order, is_system) VALUES
                ('{{TaskProject}}', {{t}}, 'task_type', 'PROJECT', 'Dự án', '{}', 2, true),
                ('{{TaskLeave}}', {{t}}, 'task_type', 'LEAVE', 'Nghỉ phép', '{}', 3, true),
                ('{{TaskOt}}', {{t}}, 'task_type', 'OT', 'Làm thêm', '{}', 4, true),
                ('{{TaskMeeting}}', {{t}}, 'task_type', 'MEETING', 'Họp', '{}', 5, false),
                ('{{LeaveAnnual}}', {{t}}, 'leave_type', 'ANNUAL', 'Phép năm', '{"deduct_balance":true}', 1, false),
                ('{{LevelFull}}', {{t}}, 'performance_level', 'FULL', 'Chính thức', '{"coefficient":1}', 1, false)
            ON CONFLICT DO NOTHING;
            INSERT INTO dx.departments (id, tenant_id, code, name) VALUES ('{{Department}}', {{t}}, 'WD', 'Work Dept') ON CONFLICT DO NOTHING;
            INSERT INTO dx.teams (id, tenant_id, department_id, code, name) VALUES ('{{Team}}', {{t}}, '{{Department}}', 'WT', 'Work Team') ON CONFLICT DO NOTHING
            """);
    }

    private async Task CleanupAsync()
    {
        await fx.ExecuteOwnerAsync($"""
            DELETE FROM dx.timesheet_entries WHERE tenant_id = '{BetaTenant.TenantId}' AND (source <> 'manual' OR task_type_id IN ('{TaskProject}', '{TaskMeeting}'));
            DELETE FROM dx.leave_requests WHERE tenant_id = '{BetaTenant.TenantId}';
            DELETE FROM dx.overtime_requests WHERE tenant_id = '{BetaTenant.TenantId}';
            DELETE FROM dx.attendance_records WHERE tenant_id = '{BetaTenant.TenantId}';
            DELETE FROM dx.effort_allocations WHERE tenant_id = '{BetaTenant.TenantId}';
            DELETE FROM dx.project_cost_centers WHERE tenant_id = '{BetaTenant.TenantId}';
            DELETE FROM dx.teams WHERE id = '{Team}';
            DELETE FROM dx.departments WHERE id = '{Department}';
            DELETE FROM dx.master_data WHERE id IN ('{TaskProject}', '{TaskLeave}', '{TaskOt}', '{TaskMeeting}', '{LeaveAnnual}', '{LevelFull}')
            """);
    }

    // ─── timesheets ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Timesheet_week_has_entries_day_states_and_projects()
    {
        using var response = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync($"/api/v1/timesheets?from={D(-9)}&to={D(1)}");
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("employeeId").GetGuid().ShouldBe(BetaTenant.EmployeeMember);
        data.GetProperty("canEdit").GetBoolean().ShouldBeTrue();
        data.GetProperty("rules").GetProperty("maxHours").GetDecimal().ShouldBe(24m); // TIMESHEET_MAX_HOURS is switched off for beta
        var days = data.GetProperty("days").EnumerateArray().ToList();
        days.Count.ShouldBe(11);
        days[0].GetProperty("lockReason").GetString().ShouldBe("edit_limit"); // 9 days ago > 7
        days[^1].GetProperty("lockReason").GetString().ShouldBe("future");
        days[^2].GetProperty("locked").GetBoolean().ShouldBeFalse();
        days[^3].GetProperty("approved").GetBoolean().ShouldBeTrue();
        data.GetProperty("projects")[0].GetProperty("code").GetString().ShouldBe("B-P1");
        data.GetProperty("entries").EnumerateArray().Count(e => e.GetProperty("date").GetString() == D(0)).ShouldBe(2);

        using var other = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync($"/api/v1/timesheets?employeeId={BetaTenant.EmployeeOther}&from={D(-1)}&to={D(0)}");
        await Envelope.ReadAsync(other, HttpStatusCode.Forbidden);
        using var tooLong = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync($"/api/v1/timesheets?from={D(-100)}&to={D(0)}");
        await Envelope.ReadAsync(tooLong, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Save_timesheet_replaces_manual_rows_and_checks_rules()
    {
        await SeedLookupsAsync();
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var quarter = await admin.PutAsJsonAsync("/api/v1/timesheets", new
            {
                employeeId = BetaTenant.EmployeeAdmin, dates = new[] { D(0) },
                entries = new[] { new { date = D(0), taskTypeId = TaskMeeting, hours = 1.1 } },
            });
            Errors(await Envelope.ReadAsync(quarter, HttpStatusCode.BadRequest)).ShouldContainKey("entries.0");

            using var noAssignment = await admin.PutAsJsonAsync("/api/v1/timesheets", new
            {
                employeeId = BetaTenant.EmployeeAdmin, dates = new[] { D(0) },
                entries = new[] { new { date = D(0), projectId = BetaTenant.P1, hours = 2.0 } },
            });
            Errors(await Envelope.ReadAsync(noAssignment, HttpStatusCode.BadRequest)).ShouldContainKey(D(0));

            using var save = await admin.PutAsJsonAsync("/api/v1/timesheets", new
            {
                employeeId = BetaTenant.EmployeeAdmin, dates = new[] { D(0), D(-20) },
                entries = new[] { new { date = D(0), taskTypeId = TaskMeeting, hours = 2.5 } },
            });
            var data = (await Envelope.ReadAsync(save, HttpStatusCode.OK)).GetProperty("data");
            data.GetProperty("savedDates").EnumerateArray().Select(d => d.GetString()).ShouldBe([D(0)]);
            data.GetProperty("skippedDates").EnumerateArray().Select(d => d.GetString()).ShouldBe([D(-20)]); // locked
            (await fx.ScalarOwnerAsync<string>($"SELECT status FROM dx.timesheet_entries WHERE employee_id = '{BetaTenant.EmployeeAdmin}' AND task_type_id = '{TaskMeeting}'")).ShouldBe("draft");

            using var approve = await admin.PostAsJsonAsync("/api/v1/timesheets/approval",
                new { from = D(0), to = D(0), action = "approve", employeeIds = new[] { BetaTenant.EmployeeAdmin } });
            (await Envelope.ReadAsync(approve, HttpStatusCode.OK)).GetProperty("data").GetProperty("updated").GetInt32().ShouldBe(1);

            using var locked = await admin.PutAsJsonAsync("/api/v1/timesheets", new { employeeId = BetaTenant.EmployeeAdmin, dates = new[] { D(0) }, entries = Array.Empty<object>() });
            await Envelope.ReadAsync(locked, HttpStatusCode.Conflict); // the only day is approved now

            using var member = await (await fx.ClientAs(BetaTenant.MemberEmail)).PutAsJsonAsync("/api/v1/timesheets",
                new { employeeId = BetaTenant.EmployeeAdmin, dates = new[] { D(0) }, entries = Array.Empty<object>() });
            await Envelope.ReadAsync(member, HttpStatusCode.Forbidden);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    // ─── leave & OT ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Leave_request_approve_generates_entries_and_cancel_removes_them()
    {
        await SeedLookupsAsync();
        var member = await fx.ClientAs(BetaTenant.MemberEmail);
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        var day = Iso(Weekday(3));
        try
        {
            using var missing = await member.PostAsJsonAsync("/api/v1/leave-requests", new { leaveTypeId = LeaveAnnual, fromDate = day, toDate = D(-30) });
            Errors(await Envelope.ReadAsync(missing, HttpStatusCode.BadRequest)).Keys.ShouldBe(["toDate", "reason"], ignoreOrder: true);

            using var create = await member.PostAsJsonAsync("/api/v1/leave-requests", new { leaveTypeId = LeaveAnnual, fromDate = day, toDate = day, reason = "Việc riêng" });
            var created = (await Envelope.ReadAsync(create, HttpStatusCode.Created)).GetProperty("data");
            var id = created.Id();
            created.GetProperty("code").GetString().ShouldBe("NP-0001");
            created.GetProperty("days").GetDecimal().ShouldBe(1m);
            created.GetProperty("totalHours").GetDecimal().ShouldBe(8m);

            using var overlap = await member.PostAsJsonAsync("/api/v1/leave-requests", new { leaveTypeId = LeaveAnnual, fromDate = day, toDate = day, reason = "x" });
            Errors(await Envelope.ReadAsync(overlap, HttpStatusCode.BadRequest)).ShouldContainKey("fromDate");

            using var selfApprove = await member.PostAsJsonAsync($"/api/v1/leave-requests/{id}/approve", new { });
            await Envelope.ReadAsync(selfApprove, HttpStatusCode.Forbidden); // no timesheet.approve

            using var approvals = await admin.GetAsync("/api/v1/leave-requests?scope=approval&statuses=pending");
            (await Envelope.ReadAsync(approvals, HttpStatusCode.OK)).GetProperty("data").GetProperty("totalCount").GetInt32().ShouldBe(1);

            using var approve = await admin.PostAsJsonAsync($"/api/v1/leave-requests/{id}/approve", new { });
            var approved = (await Envelope.ReadAsync(approve, HttpStatusCode.OK)).GetProperty("data");
            approved.GetProperty("status").GetString().ShouldBe("approved");
            approved.GetProperty("approver").GetProperty("fullName").GetString().ShouldBe("Beta Admin");
            (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.timesheet_entries WHERE source_request_id = '{id}' AND status = 'approved'")).ShouldBe(1);

            using var summary = await member.GetAsync("/api/v1/leave-requests/summary");
            var balance = (await Envelope.ReadAsync(summary, HttpStatusCode.OK)).GetProperty("data").GetProperty("balance");
            balance.GetProperty("entitledDays").GetDecimal().ShouldBe(12m);
            balance.GetProperty("usedDays").GetDecimal().ShouldBe(Weekday(3).Year == BetaTenant.Today.Year ? 1m : 0m);

            using var cancel = await member.PostAsJsonAsync($"/api/v1/leave-requests/{id}/cancel", new { });
            (await Envelope.ReadAsync(cancel, HttpStatusCode.OK)).GetProperty("data").GetProperty("status").GetString().ShouldBe("cancelled");
            (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.timesheet_entries WHERE source_request_id = '{id}'")).ShouldBe(0);

            using var rejectNoNote = await admin.PostAsJsonAsync($"/api/v1/leave-requests/{id}/reject", new { });
            await Envelope.ReadAsync(rejectNoNote, HttpStatusCode.Conflict); // cancelled is not pending
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task Overtime_hours_rate_and_daily_limit()
    {
        await SeedLookupsAsync();
        var member = await fx.ClientAs(BetaTenant.MemberEmail);
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        var day = Weekday(-6);
        try
        {
            using var create = await member.PostAsJsonAsync("/api/v1/ot-requests",
                new { workDate = Iso(day), startTime = "18:00", endTime = "21:30", projectId = BetaTenant.P1, reason = "Release" });
            var ot = (await Envelope.ReadAsync(create, HttpStatusCode.Created)).GetProperty("data");
            ot.GetProperty("hours").GetDecimal().ShouldBe(3.5m);
            ot.GetProperty("dayKind").GetString().ShouldBe("weekday");
            ot.GetProperty("ratePercent").GetInt32().ShouldBe(150);

            using var tooMuch = await member.PostAsJsonAsync("/api/v1/ot-requests",
                new { workDate = Iso(day), startTime = "06:00", endTime = "07:00", reason = "More" });
            Errors(await Envelope.ReadAsync(tooMuch, HttpStatusCode.BadRequest)).ShouldContainKey("endTime"); // 3.5 + 1 > 4 h a day

            using var approve = await admin.PostAsJsonAsync($"/api/v1/ot-requests/{ot.Id()}/approve", new { });
            await Envelope.ReadAsync(approve, HttpStatusCode.OK);
            (await fx.ScalarOwnerAsync<string>($"SELECT source FROM dx.timesheet_entries WHERE source_request_id = '{ot.Id()}'")).ShouldBe("overtime_request");

            using var log = await member.GetAsync($"/api/v1/timesheets?from={Iso(day)}&to={Iso(day)}");
            (await Envelope.ReadAsync(log, HttpStatusCode.OK)).GetProperty("data").GetProperty("entries").EnumerateArray()
                .ShouldContain(e => e.GetProperty("source").GetString() == "ot_request");

            using var reject = await admin.PostAsJsonAsync($"/api/v1/ot-requests/{ot.Id()}/reject", new { note = "Không cần" });
            (await Envelope.ReadAsync(reject, HttpStatusCode.OK)).GetProperty("data").GetProperty("status").GetString().ShouldBe("rejected");
            (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.timesheet_entries WHERE source_request_id = '{ot.Id()}'")).ShouldBe(0);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    // ─── attendance ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Attendance_check_in_out_corrections_and_import()
    {
        var member = await fx.ClientAs(BetaTenant.MemberEmail);
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var checkIn = await member.PostAsync("/api/v1/attendance/check-in", null);
            (await Envelope.ReadAsync(checkIn, HttpStatusCode.OK)).GetProperty("data").GetProperty("source").GetString().ShouldBe("app");
            using var again = await member.PostAsync("/api/v1/attendance/check-in", null);
            await Envelope.ReadAsync(again, HttpStatusCode.Conflict);
            using var checkOut = await member.PostAsync("/api/v1/attendance/check-out", null);
            (await Envelope.ReadAsync(checkOut, HttpStatusCode.OK)).GetProperty("data").GetProperty("checkOut").GetString().ShouldNotBeNull();
            using var today = await member.GetAsync("/api/v1/attendance/today");
            var t = (await Envelope.ReadAsync(today, HttpStatusCode.OK)).GetProperty("data");
            t.GetProperty("allowAppCheckIn").GetBoolean().ShouldBeTrue();
            t.GetProperty("record").GetProperty("checkIn").GetString().ShouldNotBeNull();

            using var self = await member.PutAsJsonAsync("/api/v1/attendance", new { date = D(-1), checkIn = "09:10", checkOut = "18:00" });
            var selfData = (await Envelope.ReadAsync(self, HttpStatusCode.OK)).GetProperty("data");
            selfData.GetProperty("late").GetBoolean().ShouldBeTrue(); // after 08:30 + 15 min
            selfData.GetProperty("note").ValueKind.ShouldBe(JsonValueKind.Null);
            using var overwrite = await member.PutAsJsonAsync("/api/v1/attendance", new { date = D(-1), checkIn = "08:00", checkOut = "18:00" });
            Errors(await Envelope.ReadAsync(overwrite, HttpStatusCode.BadRequest)).ShouldContainKey("checkIn");

            using var noNote = await admin.PutAsJsonAsync("/api/v1/attendance", new { employeeId = BetaTenant.EmployeeMember, date = D(-2), checkIn = "08:00" });
            Errors(await Envelope.ReadAsync(noNote, HttpStatusCode.BadRequest)).ShouldContainKey("note");
            using var byAdmin = await admin.PutAsJsonAsync("/api/v1/attendance", new { employeeId = BetaTenant.EmployeeMember, date = D(-2), checkIn = "08:00", checkOut = "17:00", note = "Quên bấm" });
            (await Envelope.ReadAsync(byAdmin, HttpStatusCode.OK)).GetProperty("data").GetProperty("updatedBy").GetString().ShouldBe("Beta Admin");

            using var import = await admin.PostAsJsonAsync("/api/v1/attendance/import", new
            {
                rows = new[]
                {
                    new { employeeCode = "b003", date = D(-3), checkIn = "08:20", checkOut = "17:40" },
                    new { employeeCode = "B001", date = D(-2), checkIn = "07:00", checkOut = "16:00" }, // corrected by hand → kept
                    new { employeeCode = "NOPE", date = "x", checkIn = "25:00", checkOut = "" },
                },
            });
            var result = (await Envelope.ReadAsync(import, HttpStatusCode.OK)).GetProperty("data");
            result.GetProperty("imported").GetInt32().ShouldBe(1);
            result.GetProperty("skipped").GetInt32().ShouldBe(1);
            result.GetProperty("errors")[0].GetProperty("codes").EnumerateArray().Select(c => c.GetString()).ShouldBe(["unknown_employee", "invalid_date", "invalid_time"]);

            using var month = await admin.GetAsync($"/api/v1/attendance?month={D(-3)[..7]}&search=B001");
            var grid = (await Envelope.ReadAsync(month, HttpStatusCode.OK)).GetProperty("data");
            grid.GetProperty("totalCount").GetInt32().ShouldBe(1);
            grid.GetProperty("items")[0].GetProperty("employee").GetProperty("code").GetString().ShouldBe("B001");
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.attendance_records WHERE tenant_id = '{BetaTenant.TenantId}'");
        }
    }

    // ─── effort grid ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Effort_rows_add_sync_member_fields_and_delete()
    {
        await SeedLookupsAsync();
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        var month = BetaTenant.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        try
        {
            using var add = await admin.PostAsJsonAsync("/api/v1/effort",
                new { month, employeeId = BetaTenant.EmployeeMember, departmentId = Department, projectId = BetaTenant.P1, teamId = Team, role = "Lập trình viên" });
            var row = (await Envelope.ReadAsync(add, HttpStatusCode.Created)).GetProperty("data");
            row.GetProperty("role").GetString().ShouldBe("Lập trình viên");
            row.GetProperty("departmentName").GetString().ShouldBe("Work Dept");
            row.GetProperty("performanceLevel").GetProperty("code").GetString().ShouldBe("FULL");

            using var duplicate = await admin.PostAsJsonAsync("/api/v1/effort",
                new { month, employeeId = BetaTenant.EmployeeMember, departmentId = Department, projectId = BetaTenant.P1, teamId = Team, role = "DEV" });
            Errors(await Envelope.ReadAsync(duplicate, HttpStatusCode.BadRequest)).ShouldContainKey("role");

            using var add2 = await admin.PostAsJsonAsync("/api/v1/effort",
                new { month, employeeId = BetaTenant.EmployeeMember, departmentId = Department, projectId = BetaTenant.P2, teamId = Team, role = "DEV" });
            await Envelope.ReadAsync(add2, HttpStatusCode.Created);

            using var patch = await admin.PatchAsJsonAsync($"/api/v1/effort/{row.Id()}", new { ttEffort = 0.8, hd = 1 });
            var rows = (await Envelope.ReadAsync(patch, HttpStatusCode.OK)).GetProperty("data").EnumerateArray().ToList();
            rows.Count.ShouldBe(2);
            rows.ShouldAllBe(r => r.GetProperty("ttEffort").GetDecimal() == 0.8m); // member field copied to the other row

            using var grid = await admin.GetAsync($"/api/v1/effort?month={month}");
            var data = (await Envelope.ReadAsync(grid, HttpStatusCode.OK)).GetProperty("data");
            data.GetProperty("rows").GetArrayLength().ShouldBe(2);
            data.GetProperty("canEdit").GetBoolean().ShouldBeTrue();

            using var delete = await admin.DeleteAsync($"/api/v1/effort/{row.Id()}");
            await Envelope.ReadAsync(delete, HttpStatusCode.OK);

            using var member = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync("/api/v1/effort");
            await Envelope.ReadAsync(member, HttpStatusCode.Forbidden);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    private static Dictionary<string, JsonElement> Errors(JsonElement body)
        => body.GetProperty("data").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
}
