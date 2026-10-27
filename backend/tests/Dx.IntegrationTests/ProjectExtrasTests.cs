using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.IntegrationTests.Infrastructure;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>PCV, CSS + public survey, project admin lists, monthly report and status report on beta project P1.</summary>
public sealed class ProjectExtrasTests(ApiFixture fx)
{
    private static string P => $"/api/v1/projects/{BetaTenant.P1}";

    private static string ThisMonth => BetaTenant.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    // ─── PCV ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pcv_copies_the_template_and_scores_the_month()
    {
        var t = $"'{BetaTenant.TenantId}'";
        await fx.ExecuteOwnerAsync($"INSERT INTO dx.pcv_template_items (tenant_id, category, content, sort_order) VALUES ({t}, 'Quy trình', 'Có kế hoạch', 1), ({t}, 'Quy trình', 'Có review', 2)");
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var get = await admin.GetAsync($"{P}/pcv");
            var data = (await Envelope.ReadAsync(get, HttpStatusCode.OK)).GetProperty("data");
            var items = data.GetProperty("items").EnumerateArray().ToList();
            items.Count.ShouldBe(2);
            data.GetProperty("summary").GetProperty("tone").GetString().ShouldBe("none");
            data.GetProperty("canEdit").GetBoolean().ShouldBeTrue();

            using var noNote = await admin.PutAsJsonAsync($"{P}/pcv", new { month = ThisMonth, results = new object[] { new { itemId = items[0].Id(), passed = true }, new { itemId = items[1].Id(), passed = false } } });
            Errors(await Envelope.ReadAsync(noNote, HttpStatusCode.BadRequest)).ShouldContainKey("results.1.note");

            using var save = await admin.PutAsJsonAsync($"{P}/pcv", new { month = ThisMonth, results = new object[] { new { itemId = items[0].Id(), passed = true }, new { itemId = items[1].Id(), passed = false, note = "Thiếu" } } });
            await Envelope.ReadAsync(save, HttpStatusCode.OK);
            using var again = await admin.GetAsync($"{P}/pcv?month={ThisMonth}");
            var summary = (await Envelope.ReadAsync(again, HttpStatusCode.OK)).GetProperty("data").GetProperty("summary");
            summary.GetProperty("score").GetDecimal().ShouldBe(50m);
            summary.GetProperty("tone").GetString().ShouldBe("red");

            using var member = await (await fx.ClientAs(BetaTenant.MemberEmail)).PostAsJsonAsync($"{P}/pcv/items", new { category = "A", label = "B" });
            await Envelope.ReadAsync(member, HttpStatusCode.Forbidden);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"""
                DELETE FROM dx.project_pcv_assessments WHERE project_id = '{BetaTenant.P1}';
                DELETE FROM dx.project_pcv_items WHERE project_id = '{BetaTenant.P1}';
                DELETE FROM dx.pcv_template_items WHERE tenant_id = '{BetaTenant.TenantId}'
                """);
        }
    }

    // ─── CSS ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Css_link_public_survey_and_score()
    {
        var t = $"'{BetaTenant.TenantId}'";
        var templateId = Guid.NewGuid();
        await fx.ExecuteOwnerAsync($"""
            INSERT INTO dx.css_templates (id, tenant_id, title) VALUES ('{templateId}', {t}, 'Khảo sát');
            INSERT INTO dx.css_template_questions (tenant_id, template_id, content, is_required, sort_order) VALUES ({t}, '{templateId}', 'Chất lượng?', true, 1), ({t}, '{templateId}', 'Tiến độ?', false, 2)
            """);
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var get = await admin.GetAsync($"{P}/css");
            (await Envelope.ReadAsync(get, HttpStatusCode.OK)).GetProperty("data").GetProperty("questions").GetArrayLength().ShouldBe(2);

            using var link = await admin.PostAsJsonAsync($"{P}/css/link", new { month = ThisMonth });
            var token = (await Envelope.ReadAsync(link, HttpStatusCode.Created)).GetProperty("data").GetProperty("token").GetString()!;

            using var page = await fx.Client().GetAsync($"/api/v1/public/css-surveys/{token}");
            var survey = (await Envelope.ReadAsync(page, HttpStatusCode.OK)).GetProperty("data");
            survey.GetProperty("projectName").GetString().ShouldBe("Alpha Portal");
            var questions = survey.GetProperty("questions").EnumerateArray().ToList();

            using var missing = await fx.Client().PostAsJsonAsync($"/api/v1/public/css-surveys/{token}", new { respondent = "Khách", answers = new[] { new { questionId = questions[1].Id(), score = 4 } } });
            Errors(await Envelope.ReadAsync(missing, HttpStatusCode.BadRequest)).ShouldContainKey("answers");
            using var submit = await fx.Client().PostAsJsonAsync($"/api/v1/public/css-surveys/{token}", new { respondent = "Khách", answers = new[] { new { questionId = questions[0].Id(), score = 5 } } });
            await Envelope.ReadAsync(submit, HttpStatusCode.OK);
            using var twice = await fx.Client().PostAsJsonAsync($"/api/v1/public/css-surveys/{token}", new { respondent = "Khách", answers = new[] { new { questionId = questions[0].Id(), score = 5 } } });
            await Envelope.ReadAsync(twice, HttpStatusCode.Conflict);
            using var bad = await fx.Client().GetAsync("/api/v1/public/css-surveys/not-a-token");
            await Envelope.ReadAsync(bad, HttpStatusCode.NotFound);

            using var after = await admin.GetAsync($"{P}/css?month={ThisMonth}");
            var summary = (await Envelope.ReadAsync(after, HttpStatusCode.OK)).GetProperty("data").GetProperty("summary");
            summary.GetProperty("score").GetDecimal().ShouldBe(5m); // the optional question left empty does not count
            summary.GetProperty("tone").GetString().ShouldBe("green");
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"""
                DELETE FROM dx.project_css_surveys WHERE project_id = '{BetaTenant.P1}';
                DELETE FROM dx.project_css_questions WHERE project_id = '{BetaTenant.P1}';
                DELETE FROM dx.project_css_forms WHERE project_id = '{BetaTenant.P1}';
                DELETE FROM dx.css_templates WHERE id = '{templateId}'
                """);
        }
    }

    // ─── project admin ───────────────────────────────────────────────────────

    [Fact]
    public async Task Risks_changes_issues_and_documents()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var risk = await admin.PostAsJsonAsync($"{P}/risks", new
            {
                name = "Thiếu nhân sự", category = "resource", probability = 4, impact = 5, strategy = "mitigate", ownerId = BetaTenant.EmployeeMember,
                dueDate = BetaTenant.Today.AddDays(-1).ToString("yyyy-MM-dd"), status = "open", identifiedAt = BetaTenant.Today.AddDays(-10).ToString("yyyy-MM-dd"),
            });
            await Envelope.ReadAsync(risk, HttpStatusCode.Created);
            using var badRisk = await admin.PostAsJsonAsync($"{P}/risks", new { name = "", category = "x", probability = 9 });
            Errors(await Envelope.ReadAsync(badRisk, HttpStatusCode.BadRequest)).Keys.ShouldContain("probability");

            using var risks = await admin.GetAsync($"{P}/risks");
            var data = (await Envelope.ReadAsync(risks, HttpStatusCode.OK)).GetProperty("data");
            var item = data.GetProperty("items")[0];
            item.GetProperty("code").GetString().ShouldBe("R-01");
            item.GetProperty("score").GetInt32().ShouldBe(20);
            item.GetProperty("level").GetString().ShouldBe("critical");
            item.GetProperty("isOverdue").GetBoolean().ShouldBeTrue();
            item.GetProperty("ownerName").GetString().ShouldBe("Beta Member");
            data.GetProperty("matrix").GetProperty("4-5").GetInt32().ShouldBe(1);
            data.GetProperty("summary").GetProperty("highOrCritical").GetInt32().ShouldBe(1);

            using var change = await admin.PostAsJsonAsync($"{P}/changes", new { fileName = "CR scope", url = "https://x.test/cr", source = "customer", impactLevel = "high", impactAreas = new[] { "cost", "scope", "cost" } });
            await Envelope.ReadAsync(change, HttpStatusCode.Created);
            using var changes = await admin.GetAsync($"{P}/changes");
            var c = (await Envelope.ReadAsync(changes, HttpStatusCode.OK)).GetProperty("data").GetProperty("items")[0];
            c.GetProperty("code").GetString().ShouldBe("CR-01");
            c.GetProperty("impactAreas").EnumerateArray().Select(a => a.GetString()).ShouldBe(["scope", "cost"]);

            using var issue = await admin.PostAsJsonAsync($"{P}/issues", new { type = "sprint", kind = "task", name = "Sprint 1", url = "https://jira.test/s1" });
            await Envelope.ReadAsync(issue, HttpStatusCode.Created);

            using var group = await admin.PostAsJsonAsync($"{P}/document-groups", new { name = "Đặc tả" });
            await Envelope.ReadAsync(group, HttpStatusCode.Created);
            var groupId = await fx.ScalarOwnerAsync<Guid>($"SELECT id FROM dx.project_document_groups WHERE project_id = '{BetaTenant.P1}'");
            using var doc = await admin.PostAsJsonAsync($"{P}/documents", new { name = "SRS", url = "https://drive.test/srs", groupId });
            await Envelope.ReadAsync(doc, HttpStatusCode.Created);
            using var docs = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync($"{P}/documents");
            var docsData = (await Envelope.ReadAsync(docs, HttpStatusCode.OK)).GetProperty("data");
            docsData.GetProperty("summary").GetProperty("documentCount").GetInt32().ShouldBe(1);
            docsData.GetProperty("canEdit").GetBoolean().ShouldBeFalse();
            docsData.GetProperty("groups")[0].GetProperty("createdByName").GetString().ShouldBe("Beta Admin");

            using var memberWrite = await (await fx.ClientAs(BetaTenant.MemberEmail)).PostAsJsonAsync($"{P}/issues", new { type = "sprint", kind = "task", name = "x", url = "https://x.test" });
            await Envelope.ReadAsync(memberWrite, HttpStatusCode.Forbidden);

            var riskId = await fx.ScalarOwnerAsync<Guid>($"SELECT id FROM dx.project_risks WHERE project_id = '{BetaTenant.P1}'");
            using var delete = await admin.DeleteAsync($"{P}/risks/{riskId}");
            await Envelope.ReadAsync(delete, HttpStatusCode.OK);
            using var next = await admin.PostAsJsonAsync($"{P}/risks", new
            {
                name = "B", category = "cost", probability = 1, impact = 1, strategy = "accept", dueDate = "2030-01-01", status = "open", identifiedAt = "2029-01-01",
            });
            await Envelope.ReadAsync(next, HttpStatusCode.Created);
            (await fx.ScalarOwnerAsync<string>($"SELECT code FROM dx.project_risks WHERE project_id = '{BetaTenant.P1}' AND NOT is_deleted")).ShouldBe("R-02"); // codes are not reused
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"""
                DELETE FROM dx.project_risks WHERE project_id = '{BetaTenant.P1}';
                DELETE FROM dx.project_change_requests WHERE project_id = '{BetaTenant.P1}';
                DELETE FROM dx.project_issue_links WHERE project_id = '{BetaTenant.P1}';
                DELETE FROM dx.project_documents WHERE project_id = '{BetaTenant.P1}';
                DELETE FROM dx.project_document_groups WHERE project_id = '{BetaTenant.P1}'
                """);
        }
    }

    // ─── monthly report ──────────────────────────────────────────────────────

    [Fact]
    public async Task Monthly_report_draft_cells_sync_and_columns()
    {
        var assignment = await fx.ScalarOwnerAsync<Guid>($"SELECT id FROM dx.project_assignments WHERE project_id = '{BetaTenant.P1}' AND employee_id = '{BetaTenant.EmployeeMember}'");
        await fx.ExecuteOwnerAsync($"INSERT INTO dx.project_assignment_periods (tenant_id, assignment_id, start_date, end_date, fte_allocated) VALUES ('{BetaTenant.TenantId}', '{assignment}', '{BetaTenant.Today.AddDays(-30):yyyy-MM-dd}', '{BetaTenant.Today.AddDays(10):yyyy-MM-dd}', 1)");
        var member = await fx.ClientAs(BetaTenant.MemberEmail);
        var url = $"{P}/monthly-reports/{ThisMonth}";
        var day = $"{ThisMonth}-01";
        try
        {
            using var draft = await member.GetAsync(url);
            var d = (await Envelope.ReadAsync(draft, HttpStatusCode.OK)).GetProperty("data");
            d.GetProperty("isDraft").GetBoolean().ShouldBeTrue();
            d.GetProperty("header").GetProperty("workName").GetString().ShouldBe("Alpha Portal");
            d.GetProperty("header").GetProperty("clientName").GetString().ShouldBe("Beta Client");
            d.GetProperty("columns").GetArrayLength().ShouldBe(5);
            d.GetProperty("members")[0].GetProperty("code").GetString().ShouldBe("B001");

            using var badStep = await member.PatchAsJsonAsync($"{url}/cells", new { employeeId = BetaTenant.EmployeeMember, date = day, field = "hours", value = 7.3 });
            Errors(await Envelope.ReadAsync(badStep, HttpStatusCode.BadRequest)).ShouldContainKey("value");
            using var cell = await member.PatchAsJsonAsync($"{url}/cells", new { employeeId = BetaTenant.EmployeeMember, date = day, field = "hours", value = 7.5 });
            await Envelope.ReadAsync(cell, HttpStatusCode.OK);
            using var start = await member.PatchAsJsonAsync($"{url}/cells", new { employeeId = BetaTenant.EmployeeMember, date = day, field = "start", value = "08:15" });
            await Envelope.ReadAsync(start, HttpStatusCode.OK);

            using var saved = await member.GetAsync(url);
            var s = (await Envelope.ReadAsync(saved, HttpStatusCode.OK)).GetProperty("data");
            s.GetProperty("isDraft").GetBoolean().ShouldBeFalse();
            var cellData = s.GetProperty("members")[0].GetProperty("days")[0];
            cellData.GetProperty("hours").GetDecimal().ShouldBe(7.5m);
            cellData.GetProperty("hoursSource").GetString().ShouldBe("manual");
            cellData.GetProperty("start").GetString().ShouldBe("08:15");
            s.GetProperty("manualHourDays").GetInt32().ShouldBe(1);

            using var sync = await member.PostAsJsonAsync($"{url}/sync", new { kind = "hours" });
            await Envelope.ReadAsync(sync, HttpStatusCode.OK);

            using var col = await member.PostAsJsonAsync($"{url}/columns", new { label = "Ghi chú", type = "text", position = 1 });
            var key = (await Envelope.ReadAsync(col, HttpStatusCode.Created)).GetProperty("data").GetProperty("id").GetString()!;
            using var dup = await member.PostAsJsonAsync($"{url}/columns", new { label = "ghi chú" });
            Errors(await Envelope.ReadAsync(dup, HttpStatusCode.BadRequest)).ShouldContainKey("label");
            using var custom = await member.PatchAsJsonAsync($"{url}/cells", new { employeeId = BetaTenant.EmployeeMember, date = day, field = $"custom:{key}", value = "OK" });
            await Envelope.ReadAsync(custom, HttpStatusCode.OK);
            using var withColumn = await member.GetAsync(url);
            var w = (await Envelope.ReadAsync(withColumn, HttpStatusCode.OK)).GetProperty("data");
            w.GetProperty("columns")[1].GetProperty("id").GetString().ShouldBe(key);
            w.GetProperty("members")[0].GetProperty("days")[0].GetProperty("custom").GetProperty(key).GetString().ShouldBe("OK");
            w.GetProperty("members")[0].GetProperty("days")[0].GetProperty("hoursSource").ToString().ShouldNotBe("manual"); // synced back to the work log

            using var system = await member.DeleteAsync($"{url}/columns/hours");
            await Envelope.ReadAsync(system, HttpStatusCode.Conflict);
            using var drop = await member.DeleteAsync($"{url}/columns/{key}");
            await Envelope.ReadAsync(drop, HttpStatusCode.OK);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"""
                DELETE FROM dx.monthly_reports WHERE project_id = '{BetaTenant.P1}';
                DELETE FROM dx.project_assignment_periods WHERE assignment_id = '{assignment}'
                """);
        }
    }

    // ─── status report ───────────────────────────────────────────────────────

    [Fact]
    public async Task Status_report_template_save_and_ai_fill()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var empty = await admin.GetAsync($"{P}/status-report");
            var e = (await Envelope.ReadAsync(empty, HttpStatusCode.OK)).GetProperty("data");
            e.GetProperty("status").GetString().ShouldBe("GOOD");
            e.GetProperty("items").GetArrayLength().ShouldBe(7);

            using var fill = await admin.PostAsync($"{P}/status-report/ai-fill", null);
            (await Envelope.ReadAsync(fill, HttpStatusCode.OK)).GetProperty("data").GetProperty("status").GetString().ShouldBe("GOOD");

            using var save = await admin.PutAsJsonAsync($"{P}/status-report", new { status = "RISK", items = new[] { new { metric = "Tiến độ", content = "Trễ 1 tuần" } } });
            await Envelope.ReadAsync(save, HttpStatusCode.OK);
            using var get = await admin.GetAsync($"{P}/status-report");
            var g = (await Envelope.ReadAsync(get, HttpStatusCode.OK)).GetProperty("data");
            g.GetProperty("status").GetString().ShouldBe("RISK");
            g.GetProperty("items")[0].GetProperty("content").GetString().ShouldBe("Trễ 1 tuần");
            g.GetProperty("updatedByName").GetString().ShouldBe("Beta Admin");

            using var member = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync($"{P}/status-report");
            await Envelope.ReadAsync(member, HttpStatusCode.Forbidden);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.project_status_reports WHERE project_id = '{BetaTenant.P1}'");
        }
    }

    private static Dictionary<string, JsonElement> Errors(JsonElement body)
        => body.GetProperty("data").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
}
