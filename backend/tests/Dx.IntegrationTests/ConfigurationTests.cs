using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.IntegrationTests.Infrastructure;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>GET /master-data, GET + PUT /system-rules, GET /settings.</summary>
public sealed class ConfigurationTests(ApiFixture fx)
{
    // ─── master-data ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Master_data_filters_by_type_and_hides_inactive_rows()
    {
        var client = await fx.ClientAs(BetaTenant.MemberEmail);
        using var response = await client.GetAsync("/api/v1/master-data?type=risk_level");
        var items = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data").EnumerateArray().ToList();

        items.Select(i => i.GetProperty("code").GetString()).ShouldBe(["HIGH", "LOW"]); // OLD is inactive
        items.ShouldAllBe(i => i.GetProperty("type").GetString() == "risk_level");
        items[0].GetProperty("color").GetString().ShouldBe("#ef4444");
        items[0].GetProperty("metadata").GetProperty("color").GetString().ShouldBe("#ef4444");
        items[1].GetProperty("color").ValueKind.ShouldBe(JsonValueKind.Null);
        items[0].GetProperty("sortSeq").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Master_data_accepts_several_types_comma_separated_or_repeated()
    {
        var client = await fx.ClientAs(BetaTenant.MemberEmail);
        using var comma = await client.GetAsync("/api/v1/master-data?type=project_status,risk_level");
        using var repeated = await client.GetAsync("/api/v1/master-data?type=project_status&type=risk_level");

        var a = Codes(await Envelope.ReadAsync(comma, HttpStatusCode.OK));
        var b = Codes(await Envelope.ReadAsync(repeated, HttpStatusCode.OK));
        // ordered by type, then sort order
        a.ShouldBe(["RUNNING", "ENDING", "PLANNING", "COMPLETED", "HIGH", "LOW"]);
        b.ShouldBe(a);
    }

    [Fact]
    public async Task Master_data_without_type_returns_every_active_row_of_the_tenant_only()
    {
        var client = await fx.ClientAs(BetaTenant.MemberEmail);
        using var response = await client.GetAsync("/api/v1/master-data");
        var items = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data").EnumerateArray().ToList();

        items.Count.ShouldBe(10); // 11 rows of the beta tenant minus the inactive one; none of the demo tenant
        var demoCount = await fx.ScalarOwnerAsync<long>(
            "SELECT count(*) FROM dx.master_data m JOIN dx.tenants t ON t.id = m.tenant_id WHERE t.code = 'demo' AND m.is_active");
        demoCount.ShouldBeGreaterThan(0);

        using var demo = await (await fx.ClientAs("admin@example.com")).GetAsync("/api/v1/master-data");
        var demoItems = (await Envelope.ReadAsync(demo, HttpStatusCode.OK)).GetProperty("data").EnumerateArray().ToList();
        demoItems.Count.ShouldBe((int)demoCount);
        demoItems.ShouldNotContain(i => i.GetProperty("id").GetGuid() == BetaTenant.StatusRunning);
    }

    [Fact]
    public async Task Master_data_names_follow_accept_language_with_vietnamese_fallback()
    {
        using var en = await (await fx.ClientAs(BetaTenant.MemberEmail, "en")).GetAsync("/api/v1/master-data?type=project_status");
        using var vi = await (await fx.ClientAs(BetaTenant.MemberEmail, "vi")).GetAsync("/api/v1/master-data?type=project_status");

        Names(await Envelope.ReadAsync(en, HttpStatusCode.OK)).ShouldBe(["Running", "Ending", "Lên kế hoạch", "Completed"]); // PLANNING has no name_en
        Names(await Envelope.ReadAsync(vi, HttpStatusCode.OK)).ShouldBe(["Đang chạy", "Sắp kết thúc", "Lên kế hoạch", "Hoàn thành"]);
    }

    [Fact]
    public async Task Master_data_requires_sign_in()
    {
        using var response = await fx.Client().GetAsync("/api/v1/master-data");
        await Envelope.ReadAsync(response, HttpStatusCode.Unauthorized);
    }

    // ─── system-rules ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Rules_list_every_definition_with_the_tenant_value_or_the_default()
    {
        using var response = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync("/api/v1/system-rules");
        var rules = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data").EnumerateArray()
            .ToDictionary(r => r.GetProperty("key").GetString()!);

        rules.Count.ShouldBe((int)await fx.ScalarOwnerAsync<long>("SELECT count(*) FROM dx.system_rules"));
        rules.Keys.First().ShouldBe("TIMESHEET_MIN_HOURS"); // ordered by sort_order

        // tenant value, enabled
        var endingSoon = rules["PROJECT_ENDING_SOON_DAYS"];
        endingSoon.GetProperty("value").GetDecimal().ShouldBe(30);
        endingSoon.GetProperty("status").GetInt32().ShouldBe(1);
        endingSoon.GetProperty("updatedAt").ValueKind.ShouldBe(JsonValueKind.String);

        // tenant value, disabled → status 0 (the default applies in calculations)
        var maxHours = rules["TIMESHEET_MAX_HOURS"];
        maxHours.GetProperty("value").GetDecimal().ShouldBe(10);
        maxHours.GetProperty("defaultValue").GetDecimal().ShouldBe(12);
        maxHours.GetProperty("status").GetInt32().ShouldBe(0);

        // no tenant row → default value, status 1, never updated
        var allowFuture = rules["TIMESHEET_ALLOW_FUTURE"];
        allowFuture.GetProperty("valueType").GetString().ShouldBe("boolean");
        allowFuture.GetProperty("value").GetBoolean().ShouldBeFalse();
        allowFuture.GetProperty("status").GetInt32().ShouldBe(1);
        allowFuture.GetProperty("updatedAt").ValueKind.ShouldBe(JsonValueKind.Null);

        var fteHours = rules["TIMESHEET_FTE_HOURS"];
        fteHours.GetProperty("min").GetDecimal().ShouldBe(1);
        fteHours.GetProperty("max").GetDecimal().ShouldBe(24);
        fteHours.GetProperty("module").GetString().ShouldBe("timesheet");
    }

    [Fact]
    public async Task Rules_names_follow_accept_language()
    {
        using var response = await (await fx.ClientAs(BetaTenant.MemberEmail, "en")).GetAsync("/api/v1/system-rules");
        var rule = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data").EnumerateArray()
            .Single(r => r.GetProperty("key").GetString() == "TIMESHEET_FTE_HOURS");
        rule.GetProperty("name").GetString().ShouldBe("Hours per FTE");
        rule.GetProperty("description").GetString()!.ShouldStartWith("Working hours");
    }

    [Fact]
    public async Task Update_rule_creates_the_tenant_value_and_is_visible_only_to_that_tenant()
    {
        const string key = "PROJECT_BUDGET_WARNING_THRESHOLD";
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var put = await admin.PutAsJsonAsync($"/api/v1/system-rules/{key}", new { value = 80, status = 1 });
            var data = (await Envelope.ReadAsync(put, HttpStatusCode.OK)).GetProperty("data");
            data.GetProperty("value").GetDecimal().ShouldBe(80);
            data.GetProperty("defaultValue").GetDecimal().ShouldBe(75);
            data.GetProperty("status").GetInt32().ShouldBe(1);

            (await RuleAsync(admin, key)).GetProperty("value").GetDecimal().ShouldBe(80);
            (await fx.ScalarOwnerAsync<Guid>($"SELECT tenant_id FROM dx.tenant_rules WHERE rule_key = '{key}' AND value = '80'")).ShouldBe(BetaTenant.TenantId);

            // the demo tenant keeps its own value
            var demoValue = await fx.ScalarOwnerAsync<string>(
                $"SELECT COALESCE((SELECT r.value::text FROM dx.tenant_rules r JOIN dx.tenants t ON t.id = r.tenant_id WHERE t.code = 'demo' AND r.rule_key = '{key}'), '75')");
            (await RuleAsync(await fx.ClientAs("admin@example.com"), key)).GetProperty("value").GetRawText().ShouldBe(demoValue);

            // disable: value kept, status 0
            using var disable = await admin.PutAsJsonAsync($"/api/v1/system-rules/{key}", new { value = 80, status = 0 });
            (await Envelope.ReadAsync(disable, HttpStatusCode.OK)).GetProperty("data").GetProperty("status").GetInt32().ShouldBe(0);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.tenant_rules WHERE tenant_id = '{BetaTenant.TenantId}' AND rule_key = '{key}'");
        }
    }

    [Fact]
    public async Task Update_rule_changes_the_numbers_the_services_use()
    {
        // PROJECT_ENDING_SOON_DAYS drives /projects/summary: 30 → P1 (10 days left) + P3 (status ENDING);
        // 5 → only P3.
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var put = await admin.PutAsJsonAsync("/api/v1/system-rules/PROJECT_ENDING_SOON_DAYS", new { value = 5 });
            await Envelope.ReadAsync(put, HttpStatusCode.OK);

            using var summary = await admin.GetAsync("/api/v1/projects/summary");
            var data = (await Envelope.ReadAsync(summary, HttpStatusCode.OK)).GetProperty("data");
            data.GetProperty("endingSoonDays").GetInt32().ShouldBe(5);
            data.GetProperty("endingSoon").GetInt32().ShouldBe(1);

            // disabled tenant value → default (30) applies again
            using var disable = await admin.PutAsJsonAsync("/api/v1/system-rules/PROJECT_ENDING_SOON_DAYS", new { value = 5, status = 0 });
            await Envelope.ReadAsync(disable, HttpStatusCode.OK);
            using var again = await admin.GetAsync("/api/v1/projects/summary");
            (await Envelope.ReadAsync(again, HttpStatusCode.OK)).GetProperty("data").GetProperty("endingSoonDays").GetInt32().ShouldBe(30);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.tenant_rules SET value = '30', is_enabled = true WHERE tenant_id = '{BetaTenant.TenantId}' AND rule_key = 'PROJECT_ENDING_SOON_DAYS'");
        }
    }

    public static TheoryData<string, string, string> InvalidRuleValues => new()
    {
        { "TIMESHEET_FTE_HOURS", """{"value":null}""", "value" },
        { "TIMESHEET_FTE_HOURS", """{}""", "value" },
        { "TIMESHEET_FTE_HOURS", """{"value":"8"}""", "value" },
        { "TIMESHEET_FTE_HOURS", """{"value":0}""", "value" },
        { "TIMESHEET_FTE_HOURS", """{"value":25}""", "value" },
        { "TIMESHEET_ALLOW_FUTURE", """{"value":1}""", "value" },
        { "TIMESHEET_FTE_HOURS", """{"value":8,"status":2}""", "status" },
    };

    [Theory]
    [MemberData(nameof(InvalidRuleValues))]
    public async Task Update_rule_rejects_invalid_values_with_400_on_the_field(string key, string body, string field)
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await admin.PutAsync($"/api/v1/system-rules/{key}", content);
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.BadRequest)).GetProperty("data");
        data.GetProperty(field).GetArrayLength().ShouldBe(1);
        data.EnumerateObject().Count().ShouldBe(1);
    }

    [Fact]
    public async Task Update_rule_out_of_range_message_names_the_limit()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail, "en");
        using var response = await admin.PutAsJsonAsync("/api/v1/system-rules/TIMESHEET_FTE_HOURS", new { value = 25 });
        var message = (await Envelope.ReadAsync(response, HttpStatusCode.BadRequest)).GetProperty("data").GetProperty("value")[0].GetString()!;
        message.ShouldContain("24");
        message.ShouldNotContain("{");
    }

    [Fact]
    public async Task Update_rule_with_unknown_key_returns_404()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).PutAsJsonAsync("/api/v1/system-rules/NO_SUCH_RULE", new { value = 1 });
        await Envelope.ReadAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_rule_requires_admin_settings_permission()
    {
        using var member = await (await fx.ClientAs(BetaTenant.MemberEmail)).PutAsJsonAsync("/api/v1/system-rules/TIMESHEET_FTE_HOURS", new { value = 9 });
        await Envelope.ReadAsync(member, HttpStatusCode.Forbidden);

        using var anonymous = await fx.Client().PutAsJsonAsync("/api/v1/system-rules/TIMESHEET_FTE_HOURS", new { value = 9 });
        await Envelope.ReadAsync(anonymous, HttpStatusCode.Unauthorized);

        (await fx.ScalarOwnerAsync<string>($"SELECT value::text FROM dx.tenant_rules WHERE tenant_id = '{BetaTenant.TenantId}' AND rule_key = 'TIMESHEET_FTE_HOURS'")).ShouldBe("8");
    }

    // ─── settings ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Settings_returns_the_tenant_key_values()
    {
        using var response = await (await fx.ClientAs(BetaTenant.GuestEmail)).GetAsync("/api/v1/settings");
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");

        data.EnumerateObject().Select(p => p.Name).ShouldBe(["company_name", "currency", "work_week"]);
        data.GetProperty("company_name").GetString().ShouldBe("Beta Test Co");
        data.GetProperty("currency").GetString().ShouldBe("VND");
    }

    [Fact]
    public async Task Settings_of_another_tenant_are_not_visible()
    {
        using var response = await (await fx.ClientAs("member@example.com")).GetAsync("/api/v1/settings");
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("company_name").GetString().ShouldBe("CodeOn Demo");
        data.GetProperty("currency").GetString().ShouldBe("USD");
    }

    [Fact]
    public async Task Settings_requires_sign_in()
    {
        using var response = await fx.Client().GetAsync("/api/v1/settings");
        await Envelope.ReadAsync(response, HttpStatusCode.Unauthorized);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static async Task<JsonElement> RuleAsync(HttpClient client, string key)
    {
        using var response = await client.GetAsync("/api/v1/system-rules");
        return (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data").EnumerateArray()
            .Single(r => r.GetProperty("key").GetString() == key);
    }

    private static string?[] Codes(JsonElement body) => body.GetProperty("data").EnumerateArray().Select(i => i.GetProperty("code").GetString()).ToArray();

    private static string?[] Names(JsonElement body) => body.GetProperty("data").EnumerateArray().Select(i => i.GetProperty("name").GetString()).ToArray();
}
