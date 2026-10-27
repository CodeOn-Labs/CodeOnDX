using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.IntegrationTests.Infrastructure;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>Admin › Categories, PUT /settings, holidays, PCV criteria and CSS form.</summary>
public sealed class AdminConfigurationTests(ApiFixture fx)
{
    // ─── categories ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Categories_list_by_screen_type_order_including_inactive_rows()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).GetAsync("/api/v1/categories?pageSize=0");
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");
        var items = data.GetProperty("items").EnumerateArray().ToList();

        data.GetProperty("totalCount").GetInt32().ShouldBe(11);
        // project_status, project_level, risk_level, job_position, task_type, employee_status (CategoryService.Types order)
        items.Select(i => i.GetProperty("type").GetString()).Distinct()
            .ShouldBe(["project_status", "project_level", "risk_level", "job_position", "task_type", "employee_status"]);
        var old = items.Single(i => i.GetProperty("code").GetString() == "OLD");
        old.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        old.GetProperty("nameEn").GetString().ShouldBe("Old");
    }

    [Fact]
    public async Task Categories_filter_by_types_and_search()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var byType = await admin.GetAsync("/api/v1/categories?types=risk_level");
        Codes(await Envelope.ReadAsync(byType, HttpStatusCode.OK)).ShouldBe(["HIGH", "LOW", "OLD"]);

        using var bySearch = await admin.GetAsync("/api/v1/categories?search=develop");
        Codes(await Envelope.ReadAsync(bySearch, HttpStatusCode.OK)).ShouldBe(["DEV", "DEV"]); // job_position + task_type
    }

    [Fact]
    public async Task Categories_require_admin_settings()
    {
        using var response = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync("/api/v1/categories");
        await Envelope.ReadAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Category_create_update_delete_round_trip()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var create = await admin.PostAsJsonAsync("/api/v1/categories",
            new { type = "risk_level", code = "mid", name = "Trung bình", nameEn = "Medium", color = "#f59e0b", sortSeq = 5, metadata = new { note = "x" } });
        var created = (await Envelope.ReadAsync(create, HttpStatusCode.Created)).GetProperty("data");
        var id = created.Id();
        try
        {
            created.GetProperty("code").GetString().ShouldBe("MID");
            created.GetProperty("color").GetString().ShouldBe("#f59e0b");
            created.GetProperty("metadata").GetProperty("color").GetString().ShouldBe("#f59e0b");
            created.GetProperty("metadata").GetProperty("note").GetString().ShouldBe("x");
            created.GetProperty("isSystem").GetBoolean().ShouldBeFalse();

            using var update = await admin.PutAsJsonAsync($"/api/v1/categories/{id}",
                new { type = "risk_level", code = "MID", name = "Vừa", sortSeq = 6, isActive = false });
            var updated = (await Envelope.ReadAsync(update, HttpStatusCode.OK)).GetProperty("data");
            updated.GetProperty("name").GetString().ShouldBe("Vừa");
            updated.GetProperty("color").ValueKind.ShouldBe(JsonValueKind.Null);
            updated.GetProperty("isActive").GetBoolean().ShouldBeFalse();

            using var typeChange = await admin.PutAsJsonAsync($"/api/v1/categories/{id}", new { type = "project_level", code = "MID", name = "Vừa", sortSeq = 6 });
            Errors(await Envelope.ReadAsync(typeChange, HttpStatusCode.BadRequest)).ShouldContainKey("type");

            using var delete = await admin.DeleteAsync($"/api/v1/categories/{id}");
            await Envelope.ReadAsync(delete, HttpStatusCode.OK);
            (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.master_data WHERE id = '{id}'")).ShouldBe(0);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.master_data WHERE id = '{id}'");
        }
    }

    [Fact]
    public async Task Category_validation_errors_are_per_field()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).PostAsJsonAsync("/api/v1/categories",
            new { type = "nope", code = "bad code", name = "", color = "#12", sortSeq = 0 });
        var errors = Errors(await Envelope.ReadAsync(response, HttpStatusCode.BadRequest));
        errors.Keys.ShouldBe(["type", "code", "name", "color", "sortSeq"], ignoreOrder: true);

        using var duplicate = await (await fx.ClientAs(BetaTenant.AdminEmail)).PostAsJsonAsync("/api/v1/categories",
            new { type = "risk_level", code = "HIGH", name = "Cao 2", sortSeq = 1 });
        Errors(await Envelope.ReadAsync(duplicate, HttpStatusCode.BadRequest)).ShouldContainKey("code");

        using var coefficient = await (await fx.ClientAs(BetaTenant.AdminEmail)).PostAsJsonAsync("/api/v1/categories",
            new { type = "performance_level", code = "X", name = "X", sortSeq = 1, metadata = new { coefficient = 3 } });
        Errors(await Envelope.ReadAsync(coefficient, HttpStatusCode.BadRequest)).ShouldContainKey("coefficient");
    }

    [Fact]
    public async Task Category_in_use_cannot_be_deleted()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).DeleteAsync($"/api/v1/categories/{BetaTenant.StatusRunning}");
        await Envelope.ReadAsync(response, HttpStatusCode.Conflict);
        (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.master_data WHERE id = '{BetaTenant.StatusRunning}'")).ShouldBe(1);
    }

    [Fact]
    public async Task Category_of_another_tenant_is_not_found()
    {
        var demoId = await fx.ScalarOwnerAsync<Guid>(
            "SELECT m.id FROM dx.master_data m JOIN dx.tenants t ON t.id = m.tenant_id WHERE t.code = 'demo' AND m.type = 'risk_level' LIMIT 1");
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).PutAsJsonAsync($"/api/v1/categories/{demoId}",
            new { type = "risk_level", code = "ZZ", name = "x", sortSeq = 1 });
        await Envelope.ReadAsync(response, HttpStatusCode.NotFound);
    }

    // ─── settings ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_settings_changes_only_the_given_keys()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var put = await admin.PutAsJsonAsync("/api/v1/settings",
                new Dictionary<string, object> { ["work_start_time"] = "08:30", ["work_end_time"] = "17:30", ["lunch_break_minutes"] = 60 });
            var data = (await Envelope.ReadAsync(put, HttpStatusCode.OK)).GetProperty("data");
            data.GetProperty("work_start_time").GetString().ShouldBe("08:30");
            data.GetProperty("lunch_break_minutes").GetString().ShouldBe("60");
            data.GetProperty("company_name").GetString().ShouldBe("Beta Test Co"); // untouched
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.tenant_settings WHERE tenant_id = '{BetaTenant.TenantId}' AND key IN ('work_start_time', 'work_end_time', 'lunch_break_minutes')");
        }
    }

    [Fact]
    public async Task Update_settings_rejects_unknown_keys_and_bad_values()
    {
        using var put = await (await fx.ClientAs(BetaTenant.AdminEmail)).PutAsJsonAsync("/api/v1/settings",
            new Dictionary<string, object> { ["nope"] = "x", ["currency"] = "XXX", ["work_start_time"] = "18:00", ["work_end_time"] = "09:00", ["password_min_length"] = 3 });
        var errors = Errors(await Envelope.ReadAsync(put, HttpStatusCode.BadRequest));
        errors.Keys.ShouldBe(["nope", "currency", "work_end_time", "password_min_length"], ignoreOrder: true);

        using var member = await (await fx.ClientAs(BetaTenant.MemberEmail)).PutAsJsonAsync("/api/v1/settings", new { company_name = "x" });
        await Envelope.ReadAsync(member, HttpStatusCode.Forbidden);
    }

    // ─── holidays ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Holidays_create_list_by_year_and_delete()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var create = await admin.PostAsJsonAsync("/api/v1/holidays", new { date = "2031-09-02", name = "Quốc khánh", isPaid = true });
        var id = (await Envelope.ReadAsync(create, HttpStatusCode.Created)).GetProperty("data").Id();
        try
        {
            using var duplicate = await admin.PostAsJsonAsync("/api/v1/holidays", new { date = "2031-09-02", name = "Again" });
            Errors(await Envelope.ReadAsync(duplicate, HttpStatusCode.BadRequest)).ShouldContainKey("date");

            using var list = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync("/api/v1/holidays?year=2031");
            var items = (await Envelope.ReadAsync(list, HttpStatusCode.OK)).GetProperty("data").EnumerateArray().ToList();
            items.Count.ShouldBe(1);
            items[0].GetProperty("date").GetString().ShouldBe("2031-09-02");
            items[0].GetProperty("isPaid").GetBoolean().ShouldBeTrue();

            using var delete = await admin.DeleteAsync($"/api/v1/holidays/{id}");
            await Envelope.ReadAsync(delete, HttpStatusCode.OK);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.holidays WHERE id = '{id}'");
        }
    }

    // ─── PCV / CSS templates ─────────────────────────────────────────────────

    [Fact]
    public async Task Pcv_template_items_crud_with_soft_delete()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var create = await admin.PostAsJsonAsync("/api/v1/pcv-template-items", new { category = "Quy trình", label = "Có kế hoạch dự án" });
        var created = (await Envelope.ReadAsync(create, HttpStatusCode.Created)).GetProperty("data");
        var id = created.Id();
        try
        {
            created.GetProperty("sortSeq").GetInt32().ShouldBe(1);
            using var update = await admin.PutAsJsonAsync($"/api/v1/pcv-template-items/{id}", new { category = "Quy trình", label = "Có kế hoạch" });
            (await Envelope.ReadAsync(update, HttpStatusCode.OK)).GetProperty("data").GetProperty("label").GetString().ShouldBe("Có kế hoạch");

            using var delete = await admin.DeleteAsync($"/api/v1/pcv-template-items/{id}");
            await Envelope.ReadAsync(delete, HttpStatusCode.OK);
            using var list = await admin.GetAsync("/api/v1/pcv-template-items");
            (await Envelope.ReadAsync(list, HttpStatusCode.OK)).GetProperty("data").GetArrayLength().ShouldBe(0);
            (await fx.ScalarOwnerAsync<bool>($"SELECT is_deleted FROM dx.pcv_template_items WHERE id = '{id}'")).ShouldBeTrue();
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.pcv_template_items WHERE id = '{id}'");
        }
    }

    [Fact]
    public async Task Css_template_is_created_on_first_write_with_its_questions()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var empty = await admin.GetAsync("/api/v1/css-template");
            (await Envelope.ReadAsync(empty, HttpStatusCode.OK)).GetProperty("data").GetProperty("title").GetString().ShouldBe("");

            using var put = await admin.PutAsJsonAsync("/api/v1/css-template", new { title = "Khảo sát", intro = "Xin chào", scaleNote = "1-5", commentLabel = "Góp ý" });
            (await Envelope.ReadAsync(put, HttpStatusCode.OK)).GetProperty("data").GetProperty("title").GetString().ShouldBe("Khảo sát");

            using var q = await admin.PostAsJsonAsync("/api/v1/css-template/questions", new { label = "Chất lượng?", required = false });
            var question = (await Envelope.ReadAsync(q, HttpStatusCode.Created)).GetProperty("data");
            question.GetProperty("required").GetBoolean().ShouldBeFalse();

            using var missing = await admin.PostAsJsonAsync("/api/v1/css-template/questions", new { label = "" });
            Errors(await Envelope.ReadAsync(missing, HttpStatusCode.BadRequest)).ShouldContainKey("label");

            using var list = await admin.GetAsync("/api/v1/css-template/questions");
            (await Envelope.ReadAsync(list, HttpStatusCode.OK)).GetProperty("data").GetArrayLength().ShouldBe(1);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.css_templates WHERE tenant_id = '{BetaTenant.TenantId}'");
        }
    }

    private static List<string?> Codes(JsonElement body)
        => body.GetProperty("data").GetProperty("items").EnumerateArray().Select(i => i.GetProperty("code").GetString()).ToList();

    private static Dictionary<string, JsonElement> Errors(JsonElement body)
        => body.GetProperty("data").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
}
