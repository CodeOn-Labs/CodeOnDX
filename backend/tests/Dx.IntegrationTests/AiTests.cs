using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.IntegrationTests.Infrastructure;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>AI in mock mode (answers from mock-data/ai), insight configs, prompt templates and integrations.</summary>
public sealed class AiTests(ApiFixture fx)
{
    [Fact]
    public async Task Insights_follow_the_screen_chain_and_chat_matches_keywords()
    {
        var member = await fx.ClientAs(BetaTenant.MemberEmail, "en");
        using var insights = await member.GetAsync("/api/v1/ai/insights?screen=project_list");
        var data = (await Envelope.ReadAsync(insights, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("screenKey").GetString().ShouldBe("project_list");
        data.GetProperty("cached").GetBoolean().ShouldBeTrue();
        data.GetProperty("items").GetArrayLength().ShouldBeGreaterThan(0);

        using var bad = await member.GetAsync("/api/v1/ai/insights?screen=nope");
        await Envelope.ReadAsync(bad, HttpStatusCode.BadRequest);

        using var greeting = await member.GetAsync("/api/v1/ai/chat/greeting");
        (await Envelope.ReadAsync(greeting, HttpStatusCode.OK)).GetProperty("data").GetProperty("message").GetString().ShouldNotBeNullOrEmpty();
        using var chat = await member.PostAsJsonAsync("/api/v1/ai/chat", new { message = "Which projects are at risk?" });
        var reply = (await Envelope.ReadAsync(chat, HttpStatusCode.OK)).GetProperty("data");
        reply.GetProperty("reply").GetString().ShouldNotBeNullOrEmpty();
        reply.GetProperty("module").GetString().ShouldBe("general");
        using var empty = await member.PostAsJsonAsync("/api/v1/ai/chat", new { message = " " });
        await Envelope.ReadAsync(empty, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Insight_config_crud()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var invalid = await admin.PostAsJsonAsync("/api/v1/ai/insight-configs", new { name = "", screenKey = "nope", dataSources = new[] { "x" }, dataPrompt = "" });
        Errors(await Envelope.ReadAsync(invalid, HttpStatusCode.BadRequest)).Keys.ShouldBe(["name", "screenKey", "dataSources", "dataPrompt"], ignoreOrder: true);

        using var create = await admin.PostAsJsonAsync("/api/v1/ai/insight-configs", new
        {
            name = "Rủi ro dự án", screenKey = "project_list", triggerCondition = "budget_anomaly", dataSources = new[] { "projects", "budget" },
            allowedRoleIds = new[] { BetaTenant.AdminRoleId }, dataPrompt = "Liệt kê dự án rủi ro",
        });
        var created = (await Envelope.ReadAsync(create, HttpStatusCode.Created)).GetProperty("data");
        created.GetProperty("status").GetString().ShouldBe("active");
        created.GetProperty("triggerCondition").GetString().ShouldBe("budget_anomaly");

        using var paused = await admin.PutAsJsonAsync($"/api/v1/ai/insight-configs/{created.Id()}", new
        {
            name = "Rủi ro dự án", screenKey = "project_list", dataSources = new[] { "projects" }, dataPrompt = "x", status = "paused",
        });
        (await Envelope.ReadAsync(paused, HttpStatusCode.OK)).GetProperty("data").GetProperty("status").GetString().ShouldBe("paused");

        using var list = await admin.GetAsync("/api/v1/ai/insight-configs?statuses=paused");
        (await Envelope.ReadAsync(list, HttpStatusCode.OK)).GetProperty("data").GetProperty("totalCount").GetInt32().ShouldBe(1);

        using var preview = await admin.PostAsJsonAsync("/api/v1/ai/insight-configs/preview", new { screenKey = "project_list", dataPrompt = "x" });
        (await Envelope.ReadAsync(preview, HttpStatusCode.OK)).GetProperty("data").GetArrayLength().ShouldBeGreaterThan(0);

        using var delete = await admin.DeleteAsync($"/api/v1/ai/insight-configs/{created.Id()}");
        await Envelope.ReadAsync(delete, HttpStatusCode.OK);

        using var member = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync("/api/v1/ai/insight-configs");
        await Envelope.ReadAsync(member, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Prompt_templates_version_preview_and_last_active_guard()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var unbalanced = await admin.PostAsJsonAsync("/api/v1/ai/prompt-templates", new { name = "Chat", scope = "chat", template = "Hi {{user.full_name}" });
        Errors(await Envelope.ReadAsync(unbalanced, HttpStatusCode.BadRequest)).ShouldContainKey("template");

        using var create = await admin.PostAsJsonAsync("/api/v1/ai/prompt-templates", new { name = "Chat", scope = "chat", status = "active", template = "Bạn là trợ lý của {{company_name}}" });
        var created = (await Envelope.ReadAsync(create, HttpStatusCode.Created)).GetProperty("data");
        created.GetProperty("version").GetInt32().ShouldBe(1);
        try
        {
            using var update = await admin.PutAsJsonAsync($"/api/v1/ai/prompt-templates/{created.Id()}", new { name = "Chat", scope = "chat", status = "active", template = "v2 {{today}}" });
            (await Envelope.ReadAsync(update, HttpStatusCode.OK)).GetProperty("data").GetProperty("version").GetInt32().ShouldBe(2);

            using var preview = await admin.PostAsJsonAsync("/api/v1/ai/prompt-templates/preview", new { template = "Cty {{company_name}}, {{context.projects.total}} dự án, {{missing}}" });
            var output = (await Envelope.ReadAsync(preview, HttpStatusCode.OK)).GetProperty("data").GetProperty("output").GetString();
            output.ShouldBe("Cty Beta Test Co, 4 dự án, {{missing}}");

            using var last = await admin.DeleteAsync($"/api/v1/ai/prompt-templates/{created.Id()}");
            await Envelope.ReadAsync(last, HttpStatusCode.Conflict);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.ai_prompt_templates WHERE tenant_id = '{BetaTenant.TenantId}'");
        }
    }

    [Fact]
    public async Task Integrations_update_test_and_preview()
    {
        await fx.ExecuteOwnerAsync($"INSERT INTO dx.tenant_integrations (tenant_id, integration_key, is_enabled, use_mock) VALUES ('{BetaTenant.TenantId}', 'GitHub', false, true), ('{BetaTenant.TenantId}', 'Email', true, true)");
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var list = await admin.GetAsync("/api/v1/integrations");
            var items = (await Envelope.ReadAsync(list, HttpStatusCode.OK)).GetProperty("data").EnumerateArray().ToList();
            items.Select(i => i.GetProperty("key").GetString()).ShouldBe(["GitHub", "Email"]);
            items[0].GetProperty("settings").GetProperty("organization").GetString().ShouldBe("codeon-demo");
            items[0].GetProperty("hasSecret").GetBoolean().ShouldBeFalse();

            using var disabled = await admin.PostAsync("/api/v1/integrations/GitHub/test", null);
            await Envelope.ReadAsync(disabled, HttpStatusCode.Conflict);

            using var http = await admin.PutAsJsonAsync("/api/v1/integrations/GitHub", new { enabled = true, settings = new { organization = "acme", nope = "x" } });
            Errors(await Envelope.ReadAsync(http, HttpStatusCode.BadRequest)).ShouldContainKey("nope");
            using var update = await admin.PutAsJsonAsync("/api/v1/integrations/GitHub", new { enabled = true, settings = new { organization = "acme" } });
            (await Envelope.ReadAsync(update, HttpStatusCode.OK)).GetProperty("data").GetProperty("settings").GetProperty("organization").GetString().ShouldBe("acme");

            using var test = await admin.PostAsync("/api/v1/integrations/GitHub/test", null);
            (await Envelope.ReadAsync(test, HttpStatusCode.OK)).GetProperty("data").GetProperty("lastTestOk").GetBoolean().ShouldBeTrue();
            using var preview = await admin.GetAsync("/api/v1/integrations/GitHub/preview");
            (await Envelope.ReadAsync(preview, HttpStatusCode.OK)).GetProperty("data").GetArrayLength().ShouldBeGreaterThan(0);

            using var unknown = await admin.GetAsync("/api/v1/integrations/Slack/preview");
            await Envelope.ReadAsync(unknown, HttpStatusCode.NotFound);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.tenant_integrations WHERE tenant_id = '{BetaTenant.TenantId}'");
        }
    }

    private static Dictionary<string, JsonElement> Errors(JsonElement body)
        => body.GetProperty("data").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
}
