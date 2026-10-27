using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.IntegrationTests.Infrastructure;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>
/// AI Insight panel without an LLM: fixed notices from thresholds in Admin › Rules. Beta data:
/// P1 Alpha Portal is HIGH risk and ends in 10 days; P2 is PLANNING; P3 is ENDING.
/// </summary>
public sealed class InsightRuleTests(ApiFixture fx)
{
    private static async Task<List<JsonElement>> ItemsAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync($"/api/v1/ai/insights?{query}");
        return (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data").GetProperty("items").EnumerateArray().ToList();
    }

    private static string Id(JsonElement e) => e.GetProperty("id").GetString()!;

    [Fact]
    public async Task Project_list_notices_follow_the_data_and_the_rules()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        var items = await ItemsAsync(admin, "screen=project_list");
        items.Select(Id).ShouldBe(["project_list-high_risk", "project_list-ending_soon", "project_list-planning"]);
        items[0].GetProperty("variant").GetString().ShouldBe("red");
        items[0].GetProperty("title").GetString().ShouldBe("1 dự án rủi ro cao");
        items[0].GetProperty("content").GetString()!.ShouldStartWith("Alpha Portal");
        items[1].GetProperty("content").GetString()!.ShouldContain("Alpha Portal, Gamma_Ops");

        // Switching the rule off hides the notice it drives.
        await fx.ExecuteOwnerAsync($"UPDATE dx.tenant_rules SET is_enabled = false WHERE tenant_id = '{BetaTenant.TenantId}' AND rule_key = 'PROJECT_ENDING_SOON_DAYS'");
        try
        {
            (await ItemsAsync(admin, "screen=project_list")).Select(Id).ShouldNotContain("project_list-ending_soon");
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.tenant_rules SET is_enabled = true WHERE tenant_id = '{BetaTenant.TenantId}' AND rule_key = 'PROJECT_ENDING_SOON_DAYS'");
        }

        var en = await ItemsAsync(await fx.ClientAs(BetaTenant.MemberEmail, "en"), "screen=project_list");
        en[0].GetProperty("title").GetString().ShouldBe("1 high-risk projects"); // member only sees P1
    }

    [Fact]
    public async Task Project_detail_notices_need_the_project_and_access()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        var items = await ItemsAsync(admin, $"screen=project_detail_overview&projectId={BetaTenant.P1}");
        // P1: 3 contract positions running today, none filled by a contract slot; 2.5 FTE contract vs 1.0 assigned
        items.Select(Id).ShouldBe(["project_detail-missing_positions", "project_detail-fte_gap"]);
        items[0].GetProperty("title").GetString().ShouldBe("Thiếu 3 vị trí theo hợp đồng");
        items[1].GetProperty("title").GetString().ShouldBe("FTE thực tế thấp hơn hợp đồng 1.5");

        (await ItemsAsync(admin, "screen=project_detail_overview")).ShouldBeEmpty(); // no project → nothing to say

        using var other = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync($"/api/v1/ai/insights?screen=project_detail&projectId={BetaTenant.P2}");
        await Envelope.ReadAsync(other, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Employee_and_week_notices()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        var people = await ItemsAsync(admin, "screen=employee_list");
        var bench = people.Single(i => Id(i) == "employee_list-bench");
        bench.GetProperty("title").GetString().ShouldBe("2 nhân sự chờ việc");
        people.Select(Id).ShouldContain("employee_list-utilization");

        (await ItemsAsync(await fx.ClientAs(BetaTenant.MemberEmail), "screen=employee_list")).ShouldBeEmpty(); // no employee.view

        var week = await ItemsAsync(await fx.ClientAs(BetaTenant.MemberEmail), "screen=timesheet_week");
        week.Select(Id).ShouldContain(i => i == "timesheet_week-week_hours" || i == "timesheet_week-week_complete");
    }
}
