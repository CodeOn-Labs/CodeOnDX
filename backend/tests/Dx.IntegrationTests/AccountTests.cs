using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.Application.Auth;
using Dx.IntegrationTests.Infrastructure;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>Forgot / reset password, accepting an invitation, own profile and password.</summary>
public sealed class AccountTests(ApiFixture fx)
{
    [Fact]
    public async Task Forgot_password_answers_the_same_for_unknown_and_known_emails()
    {
        using var unknown = await fx.Client().PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "nobody@example.test" });
        var a = await Envelope.ReadAsync(unknown, HttpStatusCode.OK);
        using var known = await fx.Client().PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = BetaTenant.GuestEmail.ToUpperInvariant() });
        var b = await Envelope.ReadAsync(known, HttpStatusCode.OK);
        try
        {
            a.GetProperty("messageText").GetString().ShouldBe(b.GetProperty("messageText").GetString());
            (await fx.ScalarOwnerAsync<long>($"SELECT count(*) FROM dx.account_tokens WHERE user_id = '{BetaTenant.GuestUserId}' AND purpose = 'password_reset'")).ShouldBe(1);

            using var invalid = await fx.Client().PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "x" });
            await Envelope.ReadAsync(invalid, HttpStatusCode.BadRequest);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.account_tokens WHERE user_id = '{BetaTenant.GuestUserId}'");
        }
    }

    [Fact]
    public async Task Reset_password_link_sets_the_password_once()
    {
        var token = "reset-" + Guid.NewGuid().ToString("N");
        var original = await fx.ScalarOwnerAsync<string>($"SELECT password_hash FROM dx.users WHERE id = '{BetaTenant.GuestUserId}'");
        await InsertTokenAsync(token, "password_reset", BetaTenant.GuestEmail, $"'{BetaTenant.GuestUserId}'", "NULL", "now() + interval '30 minutes'");
        try
        {
            using var check = await fx.Client().GetAsync($"/api/v1/auth/reset-password/{token}");
            var data = (await Envelope.ReadAsync(check, HttpStatusCode.OK)).GetProperty("data");
            data.GetProperty("kind").GetString().ShouldBe("reset");
            data.GetProperty("email").GetString().ShouldBe(BetaTenant.GuestEmail);
            data.GetProperty("passwordMinLength").GetInt32().ShouldBe(8);

            using var weak = await fx.Client().PostAsJsonAsync("/api/v1/auth/reset-password", new { token, password = "short", confirmPassword = "other" });
            Errors(await Envelope.ReadAsync(weak, HttpStatusCode.BadRequest)).Keys.ShouldBe(["password", "confirmPassword"], ignoreOrder: true);

            using var ok = await fx.Client().PostAsJsonAsync("/api/v1/auth/reset-password", new { token, password = "NewPass2026", confirmPassword = "NewPass2026" });
            await Envelope.ReadAsync(ok, HttpStatusCode.OK);

            using var again = await fx.Client().GetAsync($"/api/v1/auth/reset-password/{token}");
            await Envelope.ReadAsync(again, HttpStatusCode.Gone);
            using var login = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = BetaTenant.GuestEmail, password = "NewPass2026" });
            await Envelope.ReadAsync(login, HttpStatusCode.OK);

            using var unknown = await fx.Client().GetAsync("/api/v1/auth/reset-password/does-not-exist");
            await Envelope.ReadAsync(unknown, HttpStatusCode.NotFound);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.users SET password_hash = '{original}' WHERE id = '{BetaTenant.GuestUserId}'");
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.account_tokens WHERE user_id = '{BetaTenant.GuestUserId}'");
        }
    }

    [Fact]
    public async Task Expired_link_answers_410()
    {
        var token = "old-" + Guid.NewGuid().ToString("N");
        await InsertTokenAsync(token, "password_reset", BetaTenant.GuestEmail, $"'{BetaTenant.GuestUserId}'", "NULL", "now() - interval '1 minute'");
        try
        {
            using var response = await fx.Client().PostAsJsonAsync("/api/v1/auth/reset-password", new { token, password = "NewPass2026", confirmPassword = "NewPass2026" });
            await Envelope.ReadAsync(response, HttpStatusCode.Gone);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"DELETE FROM dx.account_tokens WHERE user_id = '{BetaTenant.GuestUserId}'");
        }
    }

    [Fact]
    public async Task Accepting_an_invitation_creates_the_account_with_role_member()
    {
        var employeeId = Guid.NewGuid();
        const string email = "invitee@example.test";
        var token = "invite-" + Guid.NewGuid().ToString("N");
        await fx.ExecuteOwnerAsync($"""
            INSERT INTO dx.employees (id, tenant_id, code, full_name, email, status_id)
            VALUES ('{employeeId}', '{BetaTenant.TenantId}', 'B900', 'Người Được Mời', '{email}', '{BetaTenant.EmployeeActive}')
            """);
        await InsertTokenAsync(token, "invitation", email, "NULL", $"'{employeeId}'", "now() + interval '72 hours'");
        try
        {
            using var check = await fx.Client().GetAsync($"/api/v1/auth/reset-password/{token}");
            (await Envelope.ReadAsync(check, HttpStatusCode.OK)).GetProperty("data").GetProperty("kind").GetString().ShouldBe("invite");

            using var accept = await fx.Client().PostAsJsonAsync("/api/v1/auth/reset-password", new { token, password = "Invite2026", confirmPassword = "Invite2026" });
            await Envelope.ReadAsync(accept, HttpStatusCode.OK);

            using var login = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Invite2026" });
            var data = (await Envelope.ReadAsync(login, HttpStatusCode.OK)).GetProperty("data");
            data.GetProperty("user").GetProperty("employeeId").GetGuid().ShouldBe(employeeId);
            data.GetProperty("user").GetProperty("roles")[0].GetProperty("code").GetString().ShouldBe("member");
            data.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ShouldBe(["project.view", "timesheet.edit"]);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"""
                DELETE FROM dx.account_tokens WHERE employee_id = '{employeeId}';
                UPDATE dx.employees SET user_id = NULL WHERE id = '{employeeId}';
                DELETE FROM dx.employees WHERE id = '{employeeId}';
                DELETE FROM dx.user_roles WHERE user_id IN (SELECT id FROM dx.users WHERE normalized_email = '{email.ToUpperInvariant()}');
                DELETE FROM dx.tenant_users WHERE user_id IN (SELECT id FROM dx.users WHERE normalized_email = '{email.ToUpperInvariant()}');
                DELETE FROM dx.users WHERE normalized_email = '{email.ToUpperInvariant()}'
                """);
        }
    }

    [Fact]
    public async Task Profile_shows_hr_data_and_running_projects()
    {
        using var response = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync("/api/v1/profile");
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("email").GetString().ShouldBe(BetaTenant.MemberEmail);
        data.GetProperty("employee").GetProperty("code").GetString().ShouldBe("B001");
        data.GetProperty("employee").GetProperty("isActive").GetBoolean().ShouldBeTrue();
        data.GetProperty("projects").EnumerateArray().Select(p => p.GetProperty("code").GetString()).ShouldBe(["B-P1"]);
        data.GetProperty("roles")[0].GetProperty("code").GetString().ShouldBe("member");
    }

    [Fact]
    public async Task Update_profile_changes_only_given_fields()
    {
        var member = await fx.ClientAs(BetaTenant.MemberEmail);
        try
        {
            using var put = await member.PutAsJsonAsync("/api/v1/profile", new { phone = "0901 234 567", skills = new[] { "Vue", " " }, preferredLocale = "vi" });
            var data = (await Envelope.ReadAsync(put, HttpStatusCode.OK)).GetProperty("data");
            data.GetProperty("fullName").GetString().ShouldBe("Beta Member"); // untouched
            data.GetProperty("phone").GetString().ShouldBe("0901 234 567");
            data.GetProperty("preferredLocale").GetString().ShouldBe("vi");
            data.GetProperty("employee").GetProperty("skills").EnumerateArray().Select(s => s.GetString()).ShouldBe(["Vue"]);

            using var bad = await member.PutAsJsonAsync("/api/v1/profile", new { fullName = "", avatarUrl = "http://x/y.png", preferredLocale = "fr" });
            Errors(await Envelope.ReadAsync(bad, HttpStatusCode.BadRequest)).Keys.ShouldBe(["fullName", "avatarUrl", "preferredLocale"], ignoreOrder: true);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.users SET preferred_locale = 'en' WHERE id = '{BetaTenant.MemberUserId}'");
            await fx.ExecuteOwnerAsync($"UPDATE dx.employees SET phone = NULL, skills = '{{}}' WHERE id = '{BetaTenant.EmployeeMember}'");
        }
    }

    [Fact]
    public async Task Change_password_checks_the_current_one()
    {
        var original = await fx.ScalarOwnerAsync<string>($"SELECT password_hash FROM dx.users WHERE id = '{BetaTenant.GuestUserId}'");
        var client = await fx.ClientAs(BetaTenant.GuestEmail);
        try
        {
            using var wrong = await client.PostAsJsonAsync("/api/v1/profile/change-password",
                new { currentPassword = "nope", password = BetaTenant.Password, confirmPassword = BetaTenant.Password });
            Errors(await Envelope.ReadAsync(wrong, HttpStatusCode.BadRequest)).ShouldContainKey("currentPassword");

            using var same = await client.PostAsJsonAsync("/api/v1/profile/change-password",
                new { currentPassword = BetaTenant.Password, password = BetaTenant.Password, confirmPassword = BetaTenant.Password });
            Errors(await Envelope.ReadAsync(same, HttpStatusCode.BadRequest)).ShouldContainKey("password");

            using var ok = await client.PostAsJsonAsync("/api/v1/profile/change-password",
                new { currentPassword = BetaTenant.Password, password = "Changed2026", confirmPassword = "Changed2026" });
            await Envelope.ReadAsync(ok, HttpStatusCode.OK);
            (await fx.ScalarOwnerAsync<string>($"SELECT password_hash FROM dx.users WHERE id = '{BetaTenant.GuestUserId}'")).ShouldNotBe(original);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.users SET password_hash = '{original}' WHERE id = '{BetaTenant.GuestUserId}'");
        }
    }

    private Task InsertTokenAsync(string token, string purpose, string email, string userId, string employeeId, string expires)
        => fx.ExecuteOwnerAsync($"""
            INSERT INTO dx.account_tokens (tenant_id, purpose, token_hash, email, user_id, employee_id, expires_at)
            VALUES ('{BetaTenant.TenantId}', '{purpose}', '{AuthService.HashToken(token)}', '{email}', {userId}, {employeeId}, {expires})
            """);

    private static Dictionary<string, JsonElement> Errors(JsonElement body)
        => body.GetProperty("data").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
}
