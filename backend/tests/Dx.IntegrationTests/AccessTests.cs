using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.IntegrationTests.Infrastructure;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>
/// Roles &amp; permissions. Beta: role admin (every permission) → beta.admin only; member
/// (project.view, timesheet.edit); guest (timesheet.edit) → guest + lock users.
/// </summary>
public sealed class AccessTests(ApiFixture fx)
{
    [Fact]
    public async Task Permissions_catalog_is_ordered_and_localized()
    {
        using var response = await (await fx.ClientAs(BetaTenant.MemberEmail, "en")).GetAsync("/api/v1/permissions");
        var items = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data").EnumerateArray().ToList();
        items.Count.ShouldBe((int)await fx.ScalarOwnerAsync<long>("SELECT count(*) FROM dx.permissions"));
        items.Select(i => i.GetProperty("sortSeq").GetInt32()).ShouldBeInOrder();
        items.ShouldContain(i => i.GetProperty("key").GetString() == "admin.roles");
        items[0].GetProperty("group").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Roles_list_with_permissions_and_member_count()
    {
        using var response = await (await fx.ClientAs(BetaTenant.MemberEmail, "en")).GetAsync("/api/v1/roles");
        var items = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data").GetProperty("items").EnumerateArray().ToList();

        items.Select(i => i.GetProperty("name").GetString()).ShouldBe(["Administrator", "Guest", "Member"]);
        var member = items.Single(i => i.GetProperty("code").GetString() == "member");
        member.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ShouldBe(["project.view", "timesheet.edit"]);
        member.GetProperty("memberCount").GetInt32().ShouldBe(1);
        items.Single(i => i.GetProperty("code").GetString() == "guest").GetProperty("memberCount").GetInt32().ShouldBe(2);

        using var filtered = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync("/api/v1/roles?permissions=project.view");
        (await Envelope.ReadAsync(filtered, HttpStatusCode.OK)).GetProperty("data").GetProperty("totalCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Role_summary_needs_admin_roles()
    {
        using var member = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync("/api/v1/roles/summary");
        await Envelope.ReadAsync(member, HttpStatusCode.Forbidden);

        using var admin = await (await fx.ClientAs(BetaTenant.AdminEmail)).GetAsync("/api/v1/roles/summary");
        var data = (await Envelope.ReadAsync(admin, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("total").GetInt32().ShouldBe(3);
        data.GetProperty("assignedUsers").GetInt32().ShouldBe(4);
    }

    [Fact]
    public async Task Custom_role_create_assign_update_delete()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var create = await admin.PostAsJsonAsync("/api/v1/roles", new { name = "Kế toán", description = "Xem tài chính", permissions = new[] { "report.finance", "project.view", "project.view" } });
        var created = (await Envelope.ReadAsync(create, HttpStatusCode.Created)).GetProperty("data");
        var id = created.Id();
        try
        {
            created.GetProperty("code").GetString()!.ShouldStartWith("custom_");
            created.GetProperty("isSystem").GetBoolean().ShouldBeFalse();
            created.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ShouldBe(["project.view", "report.finance"]);

            using var duplicate = await admin.PostAsJsonAsync("/api/v1/roles", new { name = "kế toán", permissions = new[] { "project.view" } });
            Errors(await Envelope.ReadAsync(duplicate, HttpStatusCode.BadRequest)).ShouldContainKey("name");
            using var noPermission = await admin.PostAsJsonAsync("/api/v1/roles", new { name = "X", permissions = Array.Empty<string>() });
            Errors(await Envelope.ReadAsync(noPermission, HttpStatusCode.BadRequest)).ShouldContainKey("permissions");

            using var assign = await admin.PostAsJsonAsync($"/api/v1/roles/{id}/users", new { userId = BetaTenant.MemberUserId });
            (await Envelope.ReadAsync(assign, HttpStatusCode.OK)).GetProperty("data")[0].Id().ShouldBe(BetaTenant.MemberUserId);
            using var unknownUser = await admin.PostAsJsonAsync($"/api/v1/roles/{id}/users", new { userId = Guid.NewGuid() });
            await Envelope.ReadAsync(unknownUser, HttpStatusCode.NotFound);

            using var update = await admin.PutAsJsonAsync($"/api/v1/roles/{id}", new { name = "Kế toán trưởng", permissions = new[] { "report.finance" } });
            var updated = (await Envelope.ReadAsync(update, HttpStatusCode.OK)).GetProperty("data");
            updated.GetProperty("name").GetString().ShouldBe("Kế toán trưởng");
            updated.GetProperty("memberCount").GetInt32().ShouldBe(1);

            using var unassign = await admin.DeleteAsync($"/api/v1/roles/{id}/users/{BetaTenant.MemberUserId}");
            (await Envelope.ReadAsync(unassign, HttpStatusCode.OK)).GetProperty("data").GetArrayLength().ShouldBe(0);

            using var delete = await admin.DeleteAsync($"/api/v1/roles/{id}");
            await Envelope.ReadAsync(delete, HttpStatusCode.OK);
            (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.roles WHERE id = '{id}'")).ShouldBe(0);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.roles WHERE id = '{id}'");
        }
    }

    [Fact]
    public async Task System_role_cannot_be_deleted_or_renamed()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        await fx.ExecuteOwnerAsync($"UPDATE dx.roles SET is_system = true WHERE id = '{BetaTenant.GuestRoleId}'");
        try
        {
            using var delete = await admin.DeleteAsync($"/api/v1/roles/{BetaTenant.GuestRoleId}");
            await Envelope.ReadAsync(delete, HttpStatusCode.Conflict);
            using var rename = await admin.PutAsJsonAsync($"/api/v1/roles/{BetaTenant.GuestRoleId}", new { name = "Visitor", permissions = new[] { "timesheet.edit" } });
            Errors(await Envelope.ReadAsync(rename, HttpStatusCode.BadRequest)).ShouldContainKey("name");
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.roles SET is_system = false WHERE id = '{BetaTenant.GuestRoleId}'");
        }
    }

    [Fact]
    public async Task The_last_role_administrator_cannot_be_removed()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        using var unassign = await admin.DeleteAsync($"/api/v1/roles/{BetaTenant.AdminRoleId}/users/{BetaTenant.AdminUserId}");
        await Envelope.ReadAsync(unassign, HttpStatusCode.Conflict);

        using var update = await admin.PutAsJsonAsync($"/api/v1/roles/{BetaTenant.AdminRoleId}", new { name = "Quản trị", permissions = new[] { "project.view" } });
        await Envelope.ReadAsync(update, HttpStatusCode.Conflict);

        using var bulk = await admin.PutAsJsonAsync("/api/v1/roles/permissions",
            new { updates = new[] { new { roleId = BetaTenant.AdminRoleId, permissions = new[] { "project.view" } } } });
        await Envelope.ReadAsync(bulk, HttpStatusCode.Conflict);

        (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.role_permissions WHERE role_id = '{BetaTenant.AdminRoleId}'"))
            .ShouldBe(await fx.ScalarOwnerAsync<long>("SELECT count(*) FROM dx.permissions"));
    }

    [Fact]
    public async Task Bulk_update_changes_several_roles()
    {
        var admin = await fx.ClientAs(BetaTenant.AdminEmail);
        try
        {
            using var bulk = await admin.PutAsJsonAsync("/api/v1/roles/permissions", new
            {
                updates = new[]
                {
                    new { roleId = BetaTenant.MemberRoleId, permissions = new[] { "project.view", "timesheet.edit", "effort.view" } },
                    new { roleId = BetaTenant.GuestRoleId, permissions = new[] { "project.view" } },
                },
            });
            var data = (await Envelope.ReadAsync(bulk, HttpStatusCode.OK)).GetProperty("data").EnumerateArray().ToList();
            data.Count.ShouldBe(2);
            data[0].GetProperty("permissions").GetArrayLength().ShouldBe(3);
            data[1].GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ShouldBe(["project.view"]);

            using var invalid = await admin.PutAsJsonAsync("/api/v1/roles/permissions",
                new { updates = new[] { new { roleId = BetaTenant.MemberRoleId, permissions = new[] { "nope" } } } });
            Errors(await Envelope.ReadAsync(invalid, HttpStatusCode.BadRequest)).ShouldContainKey(BetaTenant.MemberRoleId.ToString());
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"""
                DELETE FROM dx.role_permissions WHERE role_id IN ('{BetaTenant.MemberRoleId}', '{BetaTenant.GuestRoleId}');
                INSERT INTO dx.role_permissions (tenant_id, role_id, permission_id)
                    SELECT '{BetaTenant.TenantId}', '{BetaTenant.MemberRoleId}', id FROM dx.permissions WHERE key IN ('project.view', 'timesheet.edit');
                INSERT INTO dx.role_permissions (tenant_id, role_id, permission_id)
                    SELECT '{BetaTenant.TenantId}', '{BetaTenant.GuestRoleId}', id FROM dx.permissions WHERE key = 'timesheet.edit'
                """);
        }
    }

    [Fact]
    public async Task Users_lists_only_accounts_of_the_tenant()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).GetAsync("/api/v1/users?search=beta");
        var items = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data").EnumerateArray().ToList();
        items.Count.ShouldBe(6);
        items.ShouldNotContain(i => i.GetProperty("email").GetString()!.EndsWith("@example.com"));
        items.Single(i => i.Id() == BetaTenant.DisabledUserId).GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    private static Dictionary<string, JsonElement> Errors(JsonElement body)
        => body.GetProperty("data").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
}
