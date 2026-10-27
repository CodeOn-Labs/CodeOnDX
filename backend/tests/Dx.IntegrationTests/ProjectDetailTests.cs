using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.IntegrationTests.Infrastructure;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>
/// Project detail, create / edit / trash, staffing, milestones, EE and quality on the beta tenant
/// (see <see cref="BetaTenant"/>: P1 runs today−60 … today+10, the member is assigned to P1 only).
/// </summary>
public sealed class ProjectDetailTests(ApiFixture fx)
{
    private static string D(int offset) => BetaTenant.Today.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ─── detail & access ─────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_for_a_manager_has_contract_staff_and_progress()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).GetAsync($"/api/v1/projects/{BetaTenant.P1}");
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("code").GetString().ShouldBe("B-P1");
        data.GetProperty("canManage").GetBoolean().ShouldBeTrue();
        data.GetProperty("contractStaff").GetArrayLength().ShouldBe(2);
        data.GetProperty("contractStaff")[0].GetProperty("roleCode").GetString().ShouldBe("DEV");
        data.GetProperty("contractStaffFte").GetDecimal().ShouldBe(2.5m);
        data.GetProperty("progressPercent").GetInt32().ShouldBe(85); // 60 of 70 days, rounded down
        data.GetProperty("budgetWarningThreshold").GetDecimal().ShouldBe(75m);
    }

    [Fact]
    public async Task Member_sees_only_assigned_projects_and_cannot_edit()
    {
        var member = await fx.ClientAs(BetaTenant.MemberEmail);
        using var p1 = await member.GetAsync($"/api/v1/projects/{BetaTenant.P1}");
        var data = (await Envelope.ReadAsync(p1, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("canManage").GetBoolean().ShouldBeFalse();
        data.GetProperty("contractStaff").GetArrayLength().ShouldBe(0);

        using var p2 = await member.GetAsync($"/api/v1/projects/{BetaTenant.P2}");
        await Envelope.ReadAsync(p2, HttpStatusCode.Forbidden);
        using var edit = await member.PatchAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/dates", new { startDate = D(-60), endDate = D(10) });
        await Envelope.ReadAsync(edit, HttpStatusCode.Forbidden);
        using var create = await member.PostAsJsonAsync("/api/v1/projects", new { name = "X" });
        await Envelope.ReadAsync(create, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_validation_lists_every_field()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).PostAsJsonAsync("/api/v1/projects",
            new { name = "", startDate = "2026-05-01", endDate = "2026-04-01", ticketSystemUrl = "ftp://x" });
        Errors(await Envelope.ReadAsync(response, HttpStatusCode.BadRequest)).Keys
            .ShouldBe(["name", "clientId", "gradeId", "endDate", "ticketSystemUrl", "contractStaff"], ignoreOrder: true);
    }

    [Fact]
    public async Task Create_update_trash_restore()
    {
        var levelId = Guid.NewGuid();
        await fx.ExecuteOwnerAsync($"INSERT INTO dx.master_data (id, tenant_id, type, code, name, sort_order) VALUES ('{levelId}', '{BetaTenant.TenantId}', 'hr_level', 'SENIOR', 'Senior', 1)");
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        Guid? id = null;
        try
        {
            using var create = await admin.PostAsJsonAsync("/api/v1/projects", new
            {
                name = "Dự án mới", clientId = BetaTenant.ClientId, gradeId = BetaTenant.GradeA, startDate = "2027-01-01", endDate = "2027-06-30", budget = 50000,
                contractStaff = new[] { new { roleCode = "DEV", levelId, quantity = 2, ftePerPerson = 1, pricePerMonth = 3000 } },
            });
            var created = (await Envelope.ReadAsync(create, HttpStatusCode.Created)).GetProperty("data");
            id = created.Id();
            created.GetProperty("code").GetString().ShouldBe("PRJ-01");
            created.GetProperty("status").GetProperty("code").GetString().ShouldBe("PLANNING");
            created.GetProperty("riskLevel").GetProperty("code").GetString().ShouldBe("LOW");
            created.GetProperty("contractStaffFte").GetDecimal().ShouldBe(2m);
            var staff = created.GetProperty("contractStaff")[0];
            staff.GetProperty("startDate").GetString().ShouldBe("2027-01-01"); // defaults to the project dates

            using var tooEarly = await admin.PutAsJsonAsync($"/api/v1/projects/{id}", new
            {
                name = "Dự án mới", clientId = BetaTenant.ClientId, gradeId = BetaTenant.GradeA, statusId = BetaTenant.StatusRunning,
                startDate = "2027-01-01", endDate = "2027-06-30",
                contractStaff = new[] { new { id = staff.Id(), roleCode = "DEV", levelId, quantity = 1, ftePerPerson = 1, startDate = "2026-12-01" } },
            });
            Errors(await Envelope.ReadAsync(tooEarly, HttpStatusCode.BadRequest)).ShouldContainKey("contractStaff.0.startDate");

            using var update = await admin.PutAsJsonAsync($"/api/v1/projects/{id}", new
            {
                name = "Dự án đổi tên", clientId = BetaTenant.ClientId, gradeId = BetaTenant.GradeA, statusId = BetaTenant.StatusRunning,
                startDate = "2027-01-01", endDate = "2027-06-30",
                contractStaff = new[] { new { id = staff.Id(), roleCode = "DEV", levelId, quantity = 1, ftePerPerson = 0.5 } },
            });
            var updated = (await Envelope.ReadAsync(update, HttpStatusCode.OK)).GetProperty("data");
            updated.GetProperty("name").GetString().ShouldBe("Dự án đổi tên");
            updated.GetProperty("status").GetProperty("code").GetString().ShouldBe("RUNNING");
            updated.GetProperty("contractStaffFte").GetDecimal().ShouldBe(0.5m);

            using var trash = await admin.DeleteAsync($"/api/v1/projects/{id}");
            await Envelope.ReadAsync(trash, HttpStatusCode.OK);
            using var editDeleted = await admin.PatchAsJsonAsync($"/api/v1/projects/{id}/dates", new { startDate = "2027-01-01", endDate = "2027-06-30" });
            await Envelope.ReadAsync(editDeleted, HttpStatusCode.Conflict);
            using var restore = await admin.PostAsync($"/api/v1/projects/{id}/restore", null);
            await Envelope.ReadAsync(restore, HttpStatusCode.OK);
            (await fx.ScalarOwnerAsync<bool>($"SELECT is_deleted FROM dx.projects WHERE id = '{id}'")).ShouldBeFalse();
        }
        finally
        {
            if (id is not null)
                await fx.ExecuteOwnerAsync($"""
                    DELETE FROM dx.project_contract_staff WHERE project_id = '{id}';
                    DELETE FROM dx.projects WHERE id = '{id}'
                    """);
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.master_data WHERE id = '{levelId}'");
        }
    }

    [Fact]
    public async Task Import_creates_valid_projects_and_reports_row_errors()
    {
        var levelId = Guid.NewGuid(); // default level of imported positions
        await fx.ExecuteOwnerAsync($"INSERT INTO dx.master_data (id, tenant_id, type, code, name, sort_order) VALUES ('{levelId}', '{BetaTenant.TenantId}', 'hr_level', 'JUNIOR', 'Junior', 1)");
        var admin = await fx.ClientAs(BetaTenant.AdminEmail, "en");
        var rows = new object[]
        {
            new { row = 2, values = new { name = "Imported A", client = "Beta Client", grade = "A", role = "Developer", quantity = 2, fte = 1, startDate = "2027-01-01", endDate = "2027-03-31" } },
            new { row = 3, values = new { name = "Imported B", client = "Nobody", role = "DEV" } },
        };
        try
        {
            using var response = await admin.PostAsJsonAsync("/api/v1/projects/import", new { rows });
            var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");
            data.GetProperty("created").GetInt32().ShouldBe(1);
            var error = data.GetProperty("errors")[0];
            error.GetProperty("row").GetInt32().ShouldBe(3);
            error.GetProperty("message").GetString()!.ShouldContain("Nobody");
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"""
                DELETE FROM dx.project_contract_staff WHERE project_id IN (SELECT id FROM dx.projects WHERE name = 'Imported A' AND tenant_id = '{BetaTenant.TenantId}');
                DELETE FROM dx.projects WHERE name = 'Imported A' AND tenant_id = '{BetaTenant.TenantId}';
                DELETE FROM dx.master_data WHERE id = '{levelId}'
                """);
        }
    }

    // ─── staffing ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Staffing_groups_contract_slots_and_outside_assignments()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).GetAsync($"/api/v1/projects/{BetaTenant.P1}/staffing");
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("startDate").GetString().ShouldBe(D(-60));
        data.GetProperty("fteHours").GetDecimal().ShouldBe(8m);
        var groups = data.GetProperty("groups").EnumerateArray().ToList();
        var contract = groups.Single(g => g.GetProperty("key").GetString() == "DEV::null");
        contract.GetProperty("contractCount").GetInt32().ShouldBe(3);
        contract.GetProperty("slots").GetArrayLength().ShouldBe(3);
        contract.GetProperty("slots")[0].GetProperty("series").GetArrayLength().ShouldBe(71);
        var outside = groups.Single(g => g.GetProperty("key").GetString() == "outside::DEV");
        outside.GetProperty("slots").GetArrayLength().ShouldBe(2); // member + the ended assignment of "other"

        using var mine = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync($"/api/v1/projects/{BetaTenant.P1}/staffing");
        var memberGroups = (await Envelope.ReadAsync(mine, HttpStatusCode.OK)).GetProperty("data").GetProperty("groups").EnumerateArray().ToList();
        memberGroups.Single().GetProperty("slots").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Assignment_create_conflict_update_delete()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        var slot = await fx.ScalarOwnerAsync<Guid>($"SELECT id FROM dx.project_contract_staff WHERE project_id = '{BetaTenant.P1}' AND quantity = 2");
        try
        {
            using var assignable = await admin.GetAsync($"/api/v1/projects/{BetaTenant.P1}/assignable-employees");
            (await Envelope.ReadAsync(assignable, HttpStatusCode.OK)).GetProperty("data").GetArrayLength().ShouldBe(3);

            using var overlap = await admin.PostAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/assignments", new
            {
                employeeId = BetaTenant.EmployeeAdmin, contractStaffId = slot, slotIndex = 1,
                intervals = new[] { new { startDate = D(-10), endDate = D(0), fte = 1.0 }, new { startDate = D(-5), endDate = D(5), fte = 0.5 } },
            });
            Errors(await Envelope.ReadAsync(overlap, HttpStatusCode.BadRequest)).ShouldContainKey("intervals.1.startDate");

            using var create = await admin.PostAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/assignments", new
            {
                employeeId = BetaTenant.EmployeeAdmin, contractStaffId = slot, slotIndex = 1,
                intervals = new[] { new { startDate = D(-10), endDate = D(0), fte = 1.0 }, new { startDate = D(1), endDate = D(10), fte = 0.5 } },
            });
            await Envelope.ReadAsync(create, HttpStatusCode.Created);
            var assignmentId = await fx.ScalarOwnerAsync<Guid>($"SELECT id FROM dx.project_assignments WHERE project_id = '{BetaTenant.P1}' AND employee_id = '{BetaTenant.EmployeeAdmin}'");
            (await fx.ScalarOwnerAsync<decimal>($"SELECT fte_allocated FROM dx.project_assignments WHERE id = '{assignmentId}'")).ShouldBe(1m);
            (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.project_members WHERE project_id = '{BetaTenant.P1}' AND employee_id = '{BetaTenant.EmployeeAdmin}'")).ShouldBe(1);

            using var twice = await admin.PostAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/assignments", new
            {
                employeeId = BetaTenant.EmployeeAdmin, roleInProject = "DEV", intervals = new[] { new { startDate = D(-1), endDate = D(0), fte = 1 } },
            });
            await Envelope.ReadAsync(twice, HttpStatusCode.Conflict);

            using var update = await admin.PutAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/assignments/{assignmentId}", new
            {
                intervals = new[] { new { startDate = D(-3), endDate = D(3), fte = 0.25 } },
            });
            await Envelope.ReadAsync(update, HttpStatusCode.OK);
            (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.project_assignment_periods WHERE assignment_id = '{assignmentId}'")).ShouldBe(1);

            using var outOfProject = await admin.PutAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/assignments/{assignmentId}", new
            {
                intervals = new[] { new { startDate = D(-3), endDate = D(30), fte = 0.25 } },
            });
            Errors(await Envelope.ReadAsync(outOfProject, HttpStatusCode.BadRequest)).ShouldContainKey("intervals.0.endDate");

            using var delete = await admin.DeleteAsync($"/api/v1/projects/{BetaTenant.P1}/assignments/{assignmentId}");
            await Envelope.ReadAsync(delete, HttpStatusCode.OK);
            (await fx.ScalarOwnerAsync<bool>($"SELECT is_deleted FROM dx.project_assignments WHERE id = '{assignmentId}'")).ShouldBeTrue();
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"""
                DELETE FROM dx.project_assignments WHERE project_id = '{BetaTenant.P1}' AND employee_id = '{BetaTenant.EmployeeAdmin}';
                DELETE FROM dx.project_members WHERE project_id = '{BetaTenant.P1}' AND employee_id = '{BetaTenant.EmployeeAdmin}'
                """);
        }
    }

    // ─── milestones ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Milestones_statuses_timeliness_and_justification()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            foreach (var m in new object[]
                     {
                         new { name = "Kick-off", plannedDate = D(-50), actualDate = D(-50) },
                         new { name = "Design", plannedDate = D(-30), actualDate = D(-25) },
                         new { name = "Build", plannedDate = D(-5) },
                         new { name = "UAT", plannedDate = D(5) },
                         new { name = "Go-live", plannedDate = D(9) },
                     })
            {
                using var c = await admin.PostAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/milestones", m);
                await Envelope.ReadAsync(c, HttpStatusCode.Created);
            }
            using var future = await admin.PostAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/milestones", new { name = "X", plannedDate = D(0), actualDate = D(1) });
            Errors(await Envelope.ReadAsync(future, HttpStatusCode.BadRequest)).ShouldContainKey("actualDate");

            using var list = await admin.GetAsync($"/api/v1/projects/{BetaTenant.P1}/milestones");
            var data = (await Envelope.ReadAsync(list, HttpStatusCode.OK)).GetProperty("data");
            var items = data.GetProperty("items").EnumerateArray().ToList();
            items.Select(i => i.GetProperty("status").GetString()).ShouldBe(["done-ontime", "done-late", "overdue", "next", "upcoming"]);
            items[1].GetProperty("delayDays").GetInt32().ShouldBe(5);
            items[2].GetProperty("delayDays").GetInt32().ShouldBe(5);
            var timeliness = data.GetProperty("timeliness");
            timeliness.GetProperty("dueCount").GetInt32().ShouldBe(3);
            timeliness.GetProperty("percent").GetInt32().ShouldBe(33);
            data.GetProperty("canEdit").GetBoolean().ShouldBeTrue();

            var late = items[1].Id();
            using var justify = await admin.PutAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/milestones/{late}",
                new { name = "Design", plannedDate = D(-30), actualDate = D(-25), justification = "Khách đổi yêu cầu" });
            await Envelope.ReadAsync(justify, HttpStatusCode.OK);
            using var again = await admin.GetAsync($"/api/v1/projects/{BetaTenant.P1}/milestones");
            var design = (await Envelope.ReadAsync(again, HttpStatusCode.OK)).GetProperty("data").GetProperty("items")[1];
            design.GetProperty("justification").GetString().ShouldBe("Khách đổi yêu cầu");
            design.GetProperty("justificationBy").GetString().ShouldBe("Beta Admin");

            using var delete = await admin.DeleteAsync($"/api/v1/projects/{BetaTenant.P1}/milestones/{late}");
            await Envelope.ReadAsync(delete, HttpStatusCode.OK);

            using var member = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync($"/api/v1/projects/{BetaTenant.P1}/milestones");
            (await Envelope.ReadAsync(member, HttpStatusCode.OK)).GetProperty("data").GetProperty("canEdit").GetBoolean().ShouldBeFalse();
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.project_milestones WHERE project_id = '{BetaTenant.P1}'");
        }
    }

    [Fact]
    public async Task Dates_patch_validates_order()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).PatchAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/dates",
            new { startDate = D(10), endDate = D(-60) });
        Errors(await Envelope.ReadAsync(response, HttpStatusCode.BadRequest)).ShouldContainKey("endDate");
    }

    // ─── EE & quality ────────────────────────────────────────────────────────

    [Fact]
    public async Task Ee_and_defect_rates_from_the_effort_grid()
    {
        var month = new DateOnly(BetaTenant.Today.Year, BetaTenant.Today.Month, 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var monthKey = month[..7];
        await fx.ExecuteOwnerAsync($"""
            INSERT INTO dx.effort_allocations (tenant_id, employee_id, project_id, period_month, position_id, contract_effort, actual_effort, allocation_rate)
            VALUES ('{BetaTenant.TenantId}', '{BetaTenant.EmployeeMember}', '{BetaTenant.P1}', '{month}', '{BetaTenant.PositionDev}', 1, 1.5, 60)
            """);
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var ee = await admin.GetAsync($"/api/v1/projects/{BetaTenant.P1}/ee?month={monthKey}");
            var data = (await Envelope.ReadAsync(ee, HttpStatusCode.OK)).GetProperty("data");
            var row = data.GetProperty("months").EnumerateArray().Single(m => m.GetProperty("month").GetString() == monthKey);
            row.GetProperty("eeMonth").GetDecimal().ShouldBe(90m); // 1.5 × 60 % = 0.9 MM of 1 MM
            row.GetProperty("eeMonthTone").GetString().ShouldBe("green");
            data.GetProperty("chartMonth").GetString().ShouldBe(monthKey);
            data.GetProperty("members")[0].GetProperty("code").GetString().ShouldBe("B001");

            using var put = await admin.PutAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/quality",
                new { kind = "defect", unit = "KLOC", entries = new[] { new { month = monthKey, defects = (int?)9, productSize = (decimal?)3 } } });
            var quality = (await Envelope.ReadAsync(put, HttpStatusCode.OK)).GetProperty("data");
            var q = quality.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("month").GetString() == monthKey);
            q.GetProperty("billableMM").GetDecimal().ShouldBe(0.9m);
            q.GetProperty("ratePerMM").GetDecimal().ShouldBe(10m); // 9 ÷ 0.9
            q.GetProperty("ratePerMMTone").GetString().ShouldBe("red");
            q.GetProperty("ratePerSize").GetDecimal().ShouldBe(3m);
            quality.GetProperty("unit").GetString().ShouldBe("KLOC");

            using var clear = await admin.PutAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/quality",
                new { kind = "defect", entries = new[] { new { month = monthKey, defects = (int?)null, productSize = (decimal?)null } } });
            await Envelope.ReadAsync(clear, HttpStatusCode.OK);
            (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.project_quality_metrics WHERE project_id = '{BetaTenant.P1}'")).ShouldBe(0);

            using var badMonth = await admin.PutAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/quality",
                new { kind = "defect", entries = new[] { new { month = "2099-01", defects = (int?)1, productSize = (decimal?)1 } } });
            Errors(await Envelope.ReadAsync(badMonth, HttpStatusCode.BadRequest)).ShouldContainKey("entries.0.month");

            using var memberPut = await (await fx.ClientAs(BetaTenant.MemberEmail)).PutAsJsonAsync($"/api/v1/projects/{BetaTenant.P1}/quality", new { kind = "defect" });
            await Envelope.ReadAsync(memberPut, HttpStatusCode.Forbidden);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"""
                DELETE FROM dx.effort_allocations WHERE project_id = '{BetaTenant.P1}';
                DELETE FROM dx.project_quality_metrics WHERE project_id = '{BetaTenant.P1}'
                """);
        }
    }

    private static Dictionary<string, JsonElement> Errors(JsonElement body)
        => body.GetProperty("data").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
}
