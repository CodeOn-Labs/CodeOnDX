using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.IntegrationTests.Infrastructure;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>
/// Clients, departments &amp; teams, employees. Beta employees today:
///   B001 member  P1 1.0 FTE running   → 100 % normal (overload only above 100)
///   B002 admin   P2 starts in 5 days  → 0 % bench
///   B003 other   P1 ended yesterday   → 0 % bench
/// </summary>
public sealed class OrganizationTests(ApiFixture fx)
{
    // ─── clients ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Clients_list_and_summary_count_projects()
    {
        var member = await fx.ClientAs(BetaTenant.MemberEmail);
        using var list = await member.GetAsync("/api/v1/clients");
        var items = (await Envelope.ReadAsync(list, HttpStatusCode.OK)).GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBe(1);
        items[0].GetProperty("name").GetString().ShouldBe("Beta Client");
        items[0].GetProperty("projectsCount").GetInt32().ShouldBe(1);

        using var summary = await member.GetAsync("/api/v1/clients/summary");
        var data = (await Envelope.ReadAsync(summary, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("total").GetInt32().ShouldBe(1);
        data.GetProperty("active").GetInt32().ShouldBe(1);
        data.GetProperty("totalProjects").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Client_create_trash_restore_and_delete_for_good()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var create = await admin.PostAsJsonAsync("/api/v1/clients", new { name = "Công ty Đông Á", contactEmail = "a@b.vn" });
        var created = (await Envelope.ReadAsync(create, HttpStatusCode.Created)).GetProperty("data");
        var id = created.Id();
        try
        {
            created.GetProperty("code").GetString().ShouldBe("CNGTYNG"); // derived from the ASCII letters of the name
            created.GetProperty("projectsCount").GetInt32().ShouldBe(0);

            using var duplicate = await admin.PostAsJsonAsync("/api/v1/clients", new { name = "công ty đông á" });
            Errors(await Envelope.ReadAsync(duplicate, HttpStatusCode.BadRequest)).ShouldContainKey("name");

            using var search = await admin.GetAsync("/api/v1/clients?search=dong a"); // accent-insensitive
            (await Envelope.ReadAsync(search, HttpStatusCode.OK)).GetProperty("data").GetProperty("totalCount").GetInt32().ShouldBe(1);

            using var trash = await admin.DeleteAsync($"/api/v1/clients/{id}");
            await Envelope.ReadAsync(trash, HttpStatusCode.OK);
            using var trashList = await admin.GetAsync("/api/v1/clients?deleted=true");
            (await Envelope.ReadAsync(trashList, HttpStatusCode.OK)).GetProperty("data").GetProperty("items")[0].Id().ShouldBe(id);

            using var restore = await admin.PostAsync($"/api/v1/clients/{id}/restore", null);
            (await Envelope.ReadAsync(restore, HttpStatusCode.OK)).GetProperty("data").GetProperty("isDeleted").GetBoolean().ShouldBeFalse();

            using var again = await admin.DeleteAsync($"/api/v1/clients/{id}");
            await Envelope.ReadAsync(again, HttpStatusCode.OK);
            using var forGood = await admin.DeleteAsync($"/api/v1/clients/{id}");
            await Envelope.ReadAsync(forGood, HttpStatusCode.OK);
            (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.clients WHERE id = '{id}'")).ShouldBe(0);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.clients WHERE id = '{id}'");
        }
    }

    [Fact]
    public async Task Client_writes_need_admin_settings()
    {
        using var response = await (await fx.ClientAs(BetaTenant.MemberEmail)).PostAsJsonAsync("/api/v1/clients", new { name = "X" });
        await Envelope.ReadAsync(response, HttpStatusCode.Forbidden);
    }

    // ─── departments & teams ─────────────────────────────────────────────────

    [Fact]
    public async Task Departments_and_teams_round_trip()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var dep = await admin.PostAsJsonAsync("/api/v1/departments", new { code = "eng", name = "Kỹ thuật", managerId = BetaTenant.EmployeeAdmin });
        var department = (await Envelope.ReadAsync(dep, HttpStatusCode.Created)).GetProperty("data");
        var depId = department.Id();
        Guid? teamId = null;
        try
        {
            department.GetProperty("code").GetString().ShouldBe("ENG");
            department.GetProperty("manager").GetProperty("fullName").GetString().ShouldBe("Beta Admin");

            using var team = await admin.PostAsJsonAsync("/api/v1/teams", new { departmentId = depId, code = "T1", name = "Team 1", leaderId = BetaTenant.EmployeeOther });
            teamId = (await Envelope.ReadAsync(team, HttpStatusCode.Created)).GetProperty("data").Id();
            await fx.ExecuteOwnerAsync($"UPDATE dx.employees SET department_id = '{depId}', team_id = '{teamId}' WHERE id = '{BetaTenant.EmployeeOther}'");

            using var list = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync("/api/v1/departments");
            var listed = (await Envelope.ReadAsync(list, HttpStatusCode.OK)).GetProperty("data").EnumerateArray().Single(d => d.Id() == depId);
            listed.GetProperty("memberCount").GetInt32().ShouldBe(1);
            listed.GetProperty("teams")[0].GetProperty("memberCount").GetInt32().ShouldBe(1);
            listed.GetProperty("teams")[0].GetProperty("leader").Id().ShouldBe(BetaTenant.EmployeeOther);

            using var blocked = await admin.DeleteAsync($"/api/v1/teams/{teamId}");
            await Envelope.ReadAsync(blocked, HttpStatusCode.Conflict);
            using var blockedDep = await admin.DeleteAsync($"/api/v1/departments/{depId}");
            await Envelope.ReadAsync(blockedDep, HttpStatusCode.Conflict);

            await fx.ExecuteOwnerAsync($"UPDATE dx.employees SET department_id = NULL, team_id = NULL WHERE id = '{BetaTenant.EmployeeOther}'");
            using var deleteTeam = await admin.DeleteAsync($"/api/v1/teams/{teamId}");
            await Envelope.ReadAsync(deleteTeam, HttpStatusCode.OK);
            using var deleteDep = await admin.DeleteAsync($"/api/v1/departments/{depId}");
            await Envelope.ReadAsync(deleteDep, HttpStatusCode.OK);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.employees SET department_id = NULL, team_id = NULL WHERE id = '{BetaTenant.EmployeeOther}'");
            if (teamId is not null) await fx.ExecuteOwnerAsync($"DELETE FROM dx.teams WHERE id = '{teamId}'");
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.departments WHERE id = '{depId}'");
        }
    }

    [Fact]
    public async Task Department_writes_need_employee_manage()
    {
        using var response = await (await fx.ClientAs(BetaTenant.MemberEmail)).PostAsJsonAsync("/api/v1/departments", new { code = "X", name = "X" });
        await Envelope.ReadAsync(response, HttpStatusCode.Forbidden);
    }

    // ─── employees ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Employees_list_with_allocation_and_account()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail, "en")).GetAsync("/api/v1/employees");
        var items = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data").GetProperty("items").EnumerateArray().ToList();

        items.Select(i => i.GetProperty("code").GetString()).ShouldBe(["B001", "B002", "B003"]);
        var member = items[0];
        member.GetProperty("totalFte").GetDecimal().ShouldBe(1.0m);
        member.GetProperty("allocationPercent").GetInt32().ShouldBe(100);
        member.GetProperty("allocationStatus").GetString().ShouldBe("normal");
        member.GetProperty("projects")[0].GetProperty("code").GetString().ShouldBe("B-P1");
        member.GetProperty("projects")[0].GetProperty("role").GetString().ShouldBe("Developer");
        member.GetProperty("account").GetProperty("status").GetString().ShouldBe("active");
        member.GetProperty("employmentStatus").GetProperty("code").GetString().ShouldBe("ACTIVE");

        items[1].GetProperty("allocationStatus").GetString().ShouldBe("bench"); // assignment not started yet
        items[2].GetProperty("allocationStatus").GetString().ShouldBe("bench"); // assignment ended
        items[2].GetProperty("account").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Employees_summary_and_allocation_filter()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var summary = await admin.GetAsync("/api/v1/employees/summary");
        var data = (await Envelope.ReadAsync(summary, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("total").GetInt32().ShouldBe(3);
        data.GetProperty("inProjects").GetInt32().ShouldBe(1);
        data.GetProperty("bench").GetInt32().ShouldBe(2);
        data.GetProperty("overloaded").GetInt32().ShouldBe(0);
        data.GetProperty("utilizationPercent").GetDecimal().ShouldBe(33.3m); // 1.0 FTE ÷ 3 working
        data.GetProperty("utilizationTarget").GetDecimal().ShouldBe(85m);

        using var bench = await admin.GetAsync("/api/v1/employees?allocation=bench");
        (await Envelope.ReadAsync(bench, HttpStatusCode.OK)).GetProperty("data").GetProperty("totalCount").GetInt32().ShouldBe(2);

        using var bad = await admin.GetAsync("/api/v1/employees?allocation=nope");
        await Envelope.ReadAsync(bad, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Employees_need_employee_view()
    {
        using var response = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync("/api/v1/employees");
        await Envelope.ReadAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Employee_create_invite_delete_restore()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var create = await admin.PostAsJsonAsync("/api/v1/employees", new
        {
            fullName = "Nguyễn Văn Mới", email = "Moi@Example.test", code = "b100", positionCode = "DEV",
            joinedDate = "2026-01-05", skills = new[] { " C# ", "", "SQL" },
        });
        var created = (await Envelope.ReadAsync(create, HttpStatusCode.Created)).GetProperty("data");
        var id = created.Id();
        try
        {
            created.GetProperty("code").GetString().ShouldBe("B100");
            created.GetProperty("email").GetString().ShouldBe("moi@example.test");
            created.GetProperty("position").GetProperty("code").GetString().ShouldBe("DEV");
            created.GetProperty("employmentStatus").GetProperty("code").GetString().ShouldBe("ACTIVE"); // default
            created.GetProperty("skills").EnumerateArray().Select(s => s.GetString()).ShouldBe(["C#", "SQL"]);

            using var duplicate = await admin.PostAsJsonAsync("/api/v1/employees", new
            {
                fullName = "X", email = "moi@example.test", code = "B100", positionCode = "NOPE", joinedDate = "2026-01-05", leftDate = "2025-01-01",
            });
            Errors(await Envelope.ReadAsync(duplicate, HttpStatusCode.BadRequest)).Keys
                .ShouldBe(["email", "code", "positionCode", "leftDate"], ignoreOrder: true);

            using var invite = await admin.PostAsync($"/api/v1/employees/{id}/invite", null);
            var invited = (await Envelope.ReadAsync(invite, HttpStatusCode.OK)).GetProperty("data");
            invited.GetProperty("account").GetProperty("status").GetString().ShouldBe("invited");
            (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.account_tokens WHERE employee_id = '{id}' AND purpose = 'invitation'")).ShouldBe(1);

            using var reinvite = await admin.PostAsync($"/api/v1/employees/{id}/invite", null);
            await Envelope.ReadAsync(reinvite, HttpStatusCode.OK);
            (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.account_tokens WHERE employee_id = '{id}'")).ShouldBe(1); // replaced

            using var hasAccount = await admin.PostAsync($"/api/v1/employees/{BetaTenant.EmployeeMember}/invite", null);
            await Envelope.ReadAsync(hasAccount, HttpStatusCode.Conflict);

            using var delete = await admin.DeleteAsync($"/api/v1/employees/{id}");
            await Envelope.ReadAsync(delete, HttpStatusCode.OK);
            using var detail = await admin.GetAsync($"/api/v1/employees/{id}");
            (await Envelope.ReadAsync(detail, HttpStatusCode.OK)).GetProperty("data").GetProperty("isDeleted").GetBoolean().ShouldBeTrue();

            using var restore = await admin.PostAsync($"/api/v1/employees/{id}/restore", null);
            (await Envelope.ReadAsync(restore, HttpStatusCode.OK)).GetProperty("data").GetProperty("isDeleted").GetBoolean().ShouldBeFalse();
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.account_tokens WHERE employee_id = '{id}'");
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.employees WHERE id = '{id}'");
        }
    }

    [Fact]
    public async Task Employee_with_running_assignment_cannot_be_deleted()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).DeleteAsync($"/api/v1/employees/{BetaTenant.EmployeeMember}");
        await Envelope.ReadAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Employee_detail_has_the_leave_balance_of_this_year()
    {
        var year = BetaTenant.Today.Year;
        await fx.ExecuteOwnerAsync($"INSERT INTO dx.leave_balances (tenant_id, employee_id, year, entitled_days, carried_over_days, adjusted_days) VALUES ('{BetaTenant.TenantId}', '{BetaTenant.EmployeeOther}', {year}, 12, 2, -1)");
        try
        {
            using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).GetAsync($"/api/v1/employees/{BetaTenant.EmployeeOther}");
            var balance = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data").GetProperty("leaveBalance");
            balance.GetProperty("year").GetInt32().ShouldBe(year);
            balance.GetProperty("entitledDays").GetDecimal().ShouldBe(12m);
            balance.GetProperty("adjustedDays").GetDecimal().ShouldBe(-1m);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.leave_balances WHERE employee_id = '{BetaTenant.EmployeeOther}'");
        }
    }

    private static Dictionary<string, JsonElement> Errors(JsonElement body)
        => body.GetProperty("data").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
}
