using System.Net;
using System.Text.Json;
using Dx.IntegrationTests.Infrastructure;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>GET /projects and /projects/summary over the beta tenant (see <see cref="BetaTenant"/> for the data).</summary>
public sealed class ProjectsTests(ApiFixture fx)
{
    [Fact]
    public async Task List_returns_live_projects_newest_first_with_paging_envelope()
    {
        var body = await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects");
        var data = body.GetProperty("data");

        Codes(data).ShouldBe(["B-P1", "B-P2", "B-P3", "B-P4"]); // P5 is soft-deleted
        data.GetProperty("page").GetInt32().ShouldBe(1);
        data.GetProperty("pageSize").GetInt32().ShouldBe(20);
        data.GetProperty("totalCount").GetInt32().ShouldBe(4);
    }

    [Fact]
    public async Task List_item_carries_references_and_computed_figures()
    {
        var p1 = Item(await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects"), BetaTenant.P1);

        p1.GetProperty("name").GetString().ShouldBe("Alpha Portal");
        p1.GetProperty("client").GetProperty("name").GetString().ShouldBe("Beta Client");
        p1.GetProperty("status").GetProperty("code").GetString().ShouldBe("RUNNING");
        p1.GetProperty("status").GetProperty("color").GetString().ShouldBe("#22c55e");
        p1.GetProperty("riskLevel").GetProperty("code").GetString().ShouldBe("HIGH");
        p1.GetProperty("grade").GetProperty("code").GetString().ShouldBe("A");
        p1.GetProperty("endDate").GetString().ShouldBe(BetaTenant.Today.AddDays(10).ToString("yyyy-MM-dd"));
        p1.GetProperty("budget").GetDecimal().ShouldBe(383000);
        p1.GetProperty("currencyCode").GetString().ShouldBe("USD");
        p1.GetProperty("progressPercent").GetInt32().ShouldBe(40);
        p1.GetProperty("isDeleted").GetBoolean().ShouldBeFalse();

        // 2 × 1.0 + 1 × 0.5
        p1.GetProperty("contractStaffFte").GetDecimal().ShouldBe(2.5m);
        // only the assignment running today counts
        p1.GetProperty("actualStaffFte").GetDecimal().ShouldBe(1.0m);
        // 480 approved h ÷ (8 × 20) = 3 FTE-months × 4 000 ÷ 383 000 × 100
        p1.GetProperty("budgetUsedPercent").GetDecimal().ShouldBe(3.13m);
    }

    [Fact]
    public async Task Budget_up_to_1000_is_read_as_man_months()
    {
        var items = await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects");

        var p2 = Item(items, BetaTenant.P2);
        p2.GetProperty("client").ValueKind.ShouldBe(JsonValueKind.Null);
        p2.GetProperty("grade").ValueKind.ShouldBe(JsonValueKind.Null);
        p2.GetProperty("actualStaffFte").GetDecimal().ShouldBe(0m); // assignment starts in the future
        // 160 h = 1 FTE-month × 5 000 ÷ (10 man-months × 5 000) × 100
        p2.GetProperty("budgetUsedPercent").GetDecimal().ShouldBe(10m);

        var p3 = Item(items, BetaTenant.P3); // no budget, no staff
        p3.GetProperty("riskLevel").ValueKind.ShouldBe(JsonValueKind.Null);
        p3.GetProperty("budget").ValueKind.ShouldBe(JsonValueKind.Null);
        p3.GetProperty("contractStaffFte").GetDecimal().ShouldBe(0m);
        p3.GetProperty("budgetUsedPercent").GetDecimal().ShouldBe(0m);
    }

    [Fact]
    public async Task Budget_used_follows_the_TIMESHEET_FTE_HOURS_rule()
    {
        try
        {
            // 480 h ÷ (4 × 20) = 6 FTE-months → 6 × 4 000 ÷ 383 000 × 100 = 6.27
            await fx.ExecuteOwnerAsync($"UPDATE dx.tenant_rules SET value = '4' WHERE tenant_id = '{BetaTenant.TenantId}' AND rule_key = 'TIMESHEET_FTE_HOURS'");
            var p1 = Item(await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects"), BetaTenant.P1);
            p1.GetProperty("budgetUsedPercent").GetDecimal().ShouldBe(6.27m);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.tenant_rules SET value = '8' WHERE tenant_id = '{BetaTenant.TenantId}' AND rule_key = 'TIMESHEET_FTE_HOURS'");
        }
    }

    [Fact]
    public async Task Deleted_true_lists_the_trash_only()
    {
        var data = (await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects?deleted=true")).GetProperty("data");
        Codes(data).ShouldBe(["B-P5"]);
        data.GetProperty("items")[0].GetProperty("isDeleted").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Without_project_manage_only_own_projects_are_listed()
    {
        var data = (await GetAsync(BetaTenant.MemberEmail, "/api/v1/projects")).GetProperty("data");
        Codes(data).ShouldBe(["B-P1"]);
        data.GetProperty("totalCount").GetInt32().ShouldBe(1);

        var summary = (await GetAsync(BetaTenant.MemberEmail, "/api/v1/projects/summary")).GetProperty("data");
        summary.GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Without_project_view_the_list_is_forbidden()
    {
        var guest = await fx.ClientAs(BetaTenant.GuestEmail);
        using var list = await guest.GetAsync("/api/v1/projects");
        using var summary = await guest.GetAsync("/api/v1/projects/summary");
        await Envelope.ReadAsync(list, HttpStatusCode.Forbidden);
        await Envelope.ReadAsync(summary, HttpStatusCode.Forbidden);

        using var anonymous = await fx.Client().GetAsync("/api/v1/projects");
        await Envelope.ReadAsync(anonymous, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Projects_of_another_tenant_are_never_listed()
    {
        var demo = (await GetAsync("admin@example.com", "/api/v1/projects?pageSize=0")).GetProperty("data");
        var demoTotal = await fx.ScalarOwnerAsync<long>(
            "SELECT count(*) FROM dx.projects p JOIN dx.tenants t ON t.id = p.tenant_id WHERE t.code = 'demo' AND NOT p.is_deleted");

        demo.GetProperty("totalCount").GetInt32().ShouldBe((int)demoTotal);
        demo.GetProperty("items").GetArrayLength().ShouldBe((int)demoTotal);
        Codes(demo).ShouldNotContain(c => c!.StartsWith("B-P"));
    }

    [Theory]
    [InlineData("search=portal", new[] { "B-P1" })]
    [InlineData("search=%20PORTAL%20", new[] { "B-P1" })] // trimmed, case-insensitive
    [InlineData("search=_", new[] { "B-P3" })]            // LIKE wildcards are escaped
    [InlineData("search=%25", new string[0])]
    [InlineData("search=zzz", new string[0])]
    public async Task Search_matches_part_of_the_name(string query, string[] expected)
    {
        Codes((await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects?" + query)).GetProperty("data")).ShouldBe(expected);
    }

    [Fact]
    public async Task Filters_by_status_risk_grade_client_and_dates()
    {
        async Task<string?[]> Q(string query) => Codes((await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects?" + query)).GetProperty("data"));

        (await Q($"statusIds={BetaTenant.StatusRunning},{BetaTenant.StatusEnding}")).ShouldBe(["B-P1", "B-P3"]);
        (await Q($"statusIds={BetaTenant.StatusRunning}&statusIds={BetaTenant.StatusPlanning}")).ShouldBe(["B-P1", "B-P2"]);
        (await Q($"riskLevelIds={BetaTenant.RiskLow}")).ShouldBe(["B-P2", "B-P4"]);
        (await Q($"gradeIds={BetaTenant.GradeA}")).ShouldBe(["B-P1"]);
        (await Q($"clientIds={BetaTenant.ClientId}")).ShouldBe(["B-P1"]);
        (await Q($"startFrom={BetaTenant.Today:yyyy-MM-dd}")).ShouldBe(["B-P2"]);
        (await Q($"endTo={BetaTenant.Today:yyyy-MM-dd}")).ShouldBe(["B-P4"]);
        (await Q($"statusIds={BetaTenant.StatusRunning}&riskLevelIds={BetaTenant.RiskLow}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Paging_slices_and_page_size_0_returns_all_rows()
    {
        var page2 = (await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects?page=2&pageSize=3")).GetProperty("data");
        Codes(page2).ShouldBe(["B-P4"]);
        page2.GetProperty("page").GetInt32().ShouldBe(2);
        page2.GetProperty("pageSize").GetInt32().ShouldBe(3);
        page2.GetProperty("totalCount").GetInt32().ShouldBe(4);

        var all = (await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects?page=5&pageSize=0")).GetProperty("data");
        Codes(all).Length.ShouldBe(4);
        all.GetProperty("page").GetInt32().ShouldBe(1);
        all.GetProperty("pageSize").GetInt32().ShouldBe(0);

        var beyond = (await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects?page=9&pageSize=3")).GetProperty("data");
        Codes(beyond).ShouldBeEmpty();
        beyond.GetProperty("totalCount").GetInt32().ShouldBe(4);

        var clamped = (await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects?page=-1&pageSize=100000")).GetProperty("data");
        clamped.GetProperty("page").GetInt32().ShouldBe(1);
        clamped.GetProperty("pageSize").GetInt32().ShouldBe(500);
    }

    [Theory]
    [InlineData("statusIds=abc", "statusIds")]
    [InlineData("gradeIds=1,2", "gradeIds")]
    [InlineData("clientIds=xyz", "clientIds")]
    [InlineData("riskLevelIds=-", "riskLevelIds")]
    [InlineData("startFrom=2026-13-01", "startFrom")]
    [InlineData("endTo=25/10/2026", "endTo")]
    public async Task Invalid_filter_values_return_400_on_the_field(string query, string field)
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var list = await admin.GetAsync("/api/v1/projects?" + query);
        using var summary = await admin.GetAsync("/api/v1/projects/summary?" + query);

        foreach (var response in new[] { list, summary })
        {
            var data = (await Envelope.ReadAsync(response, HttpStatusCode.BadRequest)).GetProperty("data");
            data.EnumerateObject().Select(p => p.Name).ShouldBe([field]);
        }
    }

    [Fact]
    public async Task Search_longer_than_200_characters_returns_400()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).GetAsync("/api/v1/projects?search=" + new string('a', 201));
        (await Envelope.ReadAsync(response, HttpStatusCode.BadRequest)).GetProperty("data").TryGetProperty("search", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Summary_counts_kpi_cards()
    {
        var data = (await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects/summary")).GetProperty("data");

        data.GetProperty("total").GetInt32().ShouldBe(4);
        data.GetProperty("running").GetInt32().ShouldBe(1);    // P1
        data.GetProperty("planning").GetInt32().ShouldBe(1);   // P2
        data.GetProperty("risky").GetInt32().ShouldBe(1);      // P1 (P5 is deleted)
        data.GetProperty("endingSoon").GetInt32().ShouldBe(2); // P1 by date, P3 by status; P4 already ended
        data.GetProperty("endingSoonDays").GetInt32().ShouldBe(30);
    }

    [Fact]
    public async Task Summary_uses_the_same_filters_as_the_list()
    {
        var running = (await GetAsync(BetaTenant.AdminEmail, $"/api/v1/projects/summary?riskLevelIds={BetaTenant.RiskLow}")).GetProperty("data");
        running.GetProperty("total").GetInt32().ShouldBe(2);
        running.GetProperty("planning").GetInt32().ShouldBe(1);
        running.GetProperty("endingSoon").GetInt32().ShouldBe(0);

        var trash = (await GetAsync(BetaTenant.AdminEmail, "/api/v1/projects/summary?deleted=true")).GetProperty("data");
        trash.GetProperty("total").GetInt32().ShouldBe(1);
        trash.GetProperty("risky").GetInt32().ShouldBe(1);
        trash.GetProperty("endingSoon").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Summary_of_the_demo_tenant_matches_the_database()
    {
        var data = (await GetAsync("admin@example.com", "/api/v1/projects/summary")).GetProperty("data");
        var expected = await fx.ScalarOwnerAsync<long>(
            "SELECT count(*) FROM dx.projects p JOIN dx.tenants t ON t.id = p.tenant_id WHERE t.code = 'demo' AND NOT p.is_deleted");
        data.GetProperty("total").GetInt32().ShouldBe((int)expected);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<JsonElement> GetAsync(string email, string url)
    {
        using var response = await (await fx.ClientAs(email)).GetAsync(url);
        return await Envelope.ReadAsync(response, HttpStatusCode.OK);
    }

    private static JsonElement Item(JsonElement body, Guid id)
        => body.GetProperty("data").GetProperty("items").EnumerateArray().Single(i => i.Id() == id);

    private static string?[] Codes(JsonElement data)
        => data.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("code").GetString()).ToArray();
}
