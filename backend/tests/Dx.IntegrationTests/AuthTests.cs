using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>POST /auth/login, /refresh, /logout and GET /auth/me (D2).</summary>
public sealed class AuthTests(ApiFixture fx)
{
    private const string Cookie = "dx_rt";

    [Fact]
    public async Task Login_returns_access_token_profile_permissions_and_refresh_cookie()
    {
        using var response = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = "admin@example.com", password = ApiFixture.DemoPassword });
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");

        data.GetProperty("accessToken").GetString().ShouldNotBeNullOrWhiteSpace();
        data.GetProperty("expiresIn").GetInt32().ShouldBe(15 * 60);
        var user = data.GetProperty("user");
        user.GetProperty("email").GetString().ShouldBe("admin@example.com");
        user.GetProperty("tenant").GetProperty("code").GetString().ShouldBe("demo");
        user.GetProperty("roles").EnumerateArray().Select(r => r.GetProperty("code").GetString()).ShouldBe(["admin"]);
        Strings(data.GetProperty("permissions")).ShouldContain("admin.settings");

        var cookie = SetCookie(response).ShouldNotBeNull();
        cookie.Value.Value.ShouldNotBeNullOrWhiteSpace();
        cookie.HttpOnly.ShouldBeTrue();
        cookie.Secure.ShouldBeTrue(); // test server speaks plain HTTP: Secure no longer depends on Request.IsHttps
        cookie.SameSite.ShouldBe(SameSiteMode.Strict);
        cookie.Path.Value.ShouldBe("/api/v1/auth");
        cookie.Expires.ShouldNotBeNull();
        // only the hash of the refresh token is stored
        var stored = await fx.ScalarOwnerAsync<long>(
            $"SELECT count(*) FROM dx.refresh_tokens WHERE token_hash = '{Dx.Application.Auth.AuthService.HashToken(cookie.Value.Value!)}'");
        stored.ShouldBe(1);
    }

    [Fact]
    public async Task Login_gives_exactly_the_permissions_of_the_user_roles()
    {
        using var response = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = BetaTenant.MemberEmail, password = BetaTenant.Password });
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");

        Strings(data.GetProperty("permissions")).ShouldBe(["project.view", "timesheet.edit"]);
        data.GetProperty("user").GetProperty("tenant").GetProperty("id").GetGuid().ShouldBe(BetaTenant.TenantId);
        data.GetProperty("user").GetProperty("employeeId").GetGuid().ShouldBe(BetaTenant.EmployeeMember);
    }

    [Fact]
    public async Task Login_email_is_trimmed_and_case_insensitive()
    {
        using var response = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = "  BETA.Admin@Example.TEST ", password = BetaTenant.Password });
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("user").GetProperty("id").GetGuid().ShouldBe(BetaTenant.AdminUserId);
    }

    [Fact]
    public async Task Login_with_missing_fields_returns_400_per_field()
    {
        using var response = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "" });
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.BadRequest)).GetProperty("data");
        data.GetProperty("email").GetArrayLength().ShouldBe(1);
        data.GetProperty("password").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Login_with_invalid_email_returns_400_on_email_only()
    {
        using var response = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = "not-an-email", password = "x" });
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.BadRequest)).GetProperty("data");
        data.TryGetProperty("email", out _).ShouldBeTrue();
        data.TryGetProperty("password", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Login_with_malformed_json_returns_400_envelope()
    {
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");
        using var response = await fx.Client().PostAsync("/api/v1/auth/login", content);
        var body = await Envelope.ReadAsync(response, HttpStatusCode.BadRequest);
        body.GetProperty("data").ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_401()
    {
        using var wrong = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = BetaTenant.AdminEmail, password = "nope" });
        using var unknown = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@example.test", password = "nope" });

        var a = await Envelope.ReadAsync(wrong, HttpStatusCode.Unauthorized);
        var b = await Envelope.ReadAsync(unknown, HttpStatusCode.Unauthorized);
        a.GetProperty("messageText").GetString().ShouldBe(b.GetProperty("messageText").GetString());
        a.GetProperty("data").ValueKind.ShouldBe(JsonValueKind.Null);
        SetCookie(wrong).ShouldBeNull();

        await fx.ExecuteOwnerAsync($"UPDATE dx.users SET access_failed_count = 0 WHERE id = '{BetaTenant.AdminUserId}'");
    }

    [Fact]
    public async Task Disabled_user_or_user_without_active_tenant_gets_403()
    {
        using var disabled = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = BetaTenant.DisabledEmail, password = BetaTenant.Password });
        using var noTenant = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = BetaTenant.NoTenantEmail, password = BetaTenant.Password });
        await Envelope.ReadAsync(disabled, HttpStatusCode.Forbidden);
        await Envelope.ReadAsync(noTenant, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Account_is_locked_after_5_failures_then_correct_password_is_refused()
    {
        try
        {
            for (var i = 0; i < 5; i++)
            {
                using var r = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = BetaTenant.LockEmail, password = "wrong" + i });
                await Envelope.ReadAsync(r, HttpStatusCode.Unauthorized);
            }
            var lockedUntil = await fx.ScalarOwnerAsync<DateTime>($"SELECT lockout_end FROM dx.users WHERE id = '{BetaTenant.LockUserId}'");
            lockedUntil.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(14));

            using var correct = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = BetaTenant.LockEmail, password = BetaTenant.Password });
            await Envelope.ReadAsync(correct, HttpStatusCode.Unauthorized);

            // once the lockout has passed the account works again and the counter is reset
            await fx.ExecuteOwnerAsync($"UPDATE dx.users SET lockout_end = now() - interval '1 minute' WHERE id = '{BetaTenant.LockUserId}'");
            using var after = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = BetaTenant.LockEmail, password = BetaTenant.Password });
            await Envelope.ReadAsync(after, HttpStatusCode.OK);
            (await fx.ScalarOwnerAsync<int>($"SELECT access_failed_count FROM dx.users WHERE id = '{BetaTenant.LockUserId}'")).ShouldBe(0);
            (await fx.ScalarOwnerAsync<bool>($"SELECT lockout_end IS NULL FROM dx.users WHERE id = '{BetaTenant.LockUserId}'")).ShouldBeTrue();
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.users SET lockout_end = NULL, access_failed_count = 0 WHERE id = '{BetaTenant.LockUserId}'");
        }
    }

    [Fact]
    public async Task Five_failures_from_one_ip_answer_429_even_with_the_right_password()
    {
        var client = fx.Client(clientIp: "203.0.113.7");
        for (var i = 0; i < 5; i++)
        {
            using var r = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = $"ghost{i}@example.test", password = "x" });
            await Envelope.ReadAsync(r, HttpStatusCode.Unauthorized);
        }
        using var blocked = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = BetaTenant.AdminEmail, password = BetaTenant.Password });
        await Envelope.ReadAsync(blocked, HttpStatusCode.TooManyRequests);

        // other IPs are not affected
        using var other = await fx.Client(clientIp: "203.0.113.8").PostAsJsonAsync("/api/v1/auth/login", new { email = BetaTenant.AdminEmail, password = BetaTenant.Password });
        await Envelope.ReadAsync(other, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Auth_endpoints_are_rate_limited_to_30_requests_per_minute_per_ip()
    {
        var client = fx.Client(clientIp: "203.0.113.30");
        for (var i = 0; i < 30; i++)
        {
            using var r = await client.PostAsync("/api/v1/auth/logout", null);
            await Envelope.ReadAsync(r, HttpStatusCode.OK);
        }
        using var limited = await client.PostAsync("/api/v1/auth/logout", null);
        await Envelope.ReadAsync(limited, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_reuse_of_an_old_token_revokes_the_family()
    {
        var first = await LoginCookieAsync(BetaTenant.AdminEmail);

        using var refreshed = await RefreshAsync(first);
        var data = (await Envelope.ReadAsync(refreshed, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("accessToken").GetString().ShouldNotBeNullOrWhiteSpace();
        data.GetProperty("user").GetProperty("id").GetGuid().ShouldBe(BetaTenant.AdminUserId);
        var second = SetCookie(refreshed).ShouldNotBeNull().Value.Value!;
        second.ShouldNotBe(first);

        // replaying the rotated token = theft: 401, cookie cleared, and the new token dies too
        using var replay = await RefreshAsync(first);
        await Envelope.ReadAsync(replay, HttpStatusCode.Unauthorized);
        IsDeletion(SetCookie(replay)).ShouldBeTrue();

        using var afterTheft = await RefreshAsync(second);
        await Envelope.ReadAsync(afterTheft, HttpStatusCode.Unauthorized);

        var reasons = await fx.ScalarOwnerAsync<string>(
            $"SELECT string_agg(revoked_reason, ',' ORDER BY created_at) FROM dx.refresh_tokens WHERE token_hash IN ('{Hash(first)}', '{Hash(second)}')");
        reasons.ShouldBe("rotated,reuse_detected");
    }

    [Fact]
    public void Refresh_has_its_own_rate_limit_bucket()
    {
        // Every page load refreshes once, so it must not eat the 30/min login bucket of an office NAT
        var endpoints = fx.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        string? Policy(string route) => endpoints.Single(e => e.RoutePattern.RawText == route).Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        Policy("api/v1/auth/refresh").ShouldBe(RateLimitPolicies.Refresh);
        Policy("api/v1/auth/login").ShouldBe(RateLimitPolicies.Auth);
        Policy("api/v1/auth/logout").ShouldBe(RateLimitPolicies.Auth);
    }

    [Fact]
    public async Task Refresh_without_or_with_unknown_cookie_returns_401()
    {
        using var none = await fx.Client().PostAsync("/api/v1/auth/refresh", null);
        await Envelope.ReadAsync(none, HttpStatusCode.Unauthorized);

        using var unknown = await RefreshAsync("not-a-real-token");
        await Envelope.ReadAsync(unknown, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_with_expired_token_returns_401()
    {
        var token = await LoginCookieAsync(BetaTenant.GuestEmail);
        await fx.ExecuteOwnerAsync($"UPDATE dx.refresh_tokens SET expires_at = now() - interval '1 second' WHERE token_hash = '{Hash(token)}'");
        using var response = await RefreshAsync(token);
        await Envelope.ReadAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_fails_once_the_user_left_the_tenant()
    {
        var token = await LoginCookieAsync(BetaTenant.GuestEmail);
        try
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.tenant_users SET is_active = false WHERE user_id = '{BetaTenant.GuestUserId}'");
            using var response = await RefreshAsync(token);
            await Envelope.ReadAsync(response, HttpStatusCode.Unauthorized);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.tenant_users SET is_active = true WHERE user_id = '{BetaTenant.GuestUserId}'");
        }
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_and_clears_the_cookie()
    {
        var token = await LoginCookieAsync(BetaTenant.MemberEmail);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", $"{Cookie}={token}");
        using var logout = await fx.Client().SendAsync(request);
        await Envelope.ReadAsync(logout, HttpStatusCode.OK);
        IsDeletion(SetCookie(logout)).ShouldBeTrue();

        (await fx.ScalarOwnerAsync<string>($"SELECT revoked_reason FROM dx.refresh_tokens WHERE token_hash = '{Hash(token)}'")).ShouldBe("logout");
        using var refresh = await RefreshAsync(token);
        await Envelope.ReadAsync(refresh, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_without_cookie_still_succeeds()
    {
        using var response = await fx.Client().PostAsync("/api/v1/auth/logout", null);
        await Envelope.ReadAsync(response, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_returns_the_signed_in_user_and_permissions()
    {
        using var response = await (await fx.ClientAs(BetaTenant.MemberEmail)).GetAsync("/api/v1/auth/me");
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");

        var user = data.GetProperty("user");
        user.GetProperty("id").GetGuid().ShouldBe(BetaTenant.MemberUserId);
        user.GetProperty("fullName").GetString().ShouldBe("Beta Member");
        user.GetProperty("preferredLocale").GetString().ShouldBe("en");
        user.GetProperty("avatarUrl").GetString().ShouldBe("https://example.test/a.png"); // from the employee record
        user.GetProperty("employeeId").GetGuid().ShouldBe(BetaTenant.EmployeeMember);
        user.GetProperty("tenant").GetProperty("name").GetString().ShouldBe("Beta Test Co");
        Strings(data.GetProperty("permissions")).ShouldBe(["project.view", "timesheet.edit"]);
    }

    [Fact]
    public async Task Me_localizes_role_names_and_messages_by_accept_language()
    {
        using var vi = await (await fx.ClientAs(BetaTenant.AdminEmail, "vi")).GetAsync("/api/v1/auth/me");
        using var en = await (await fx.ClientAs(BetaTenant.AdminEmail, "en-US")).GetAsync("/api/v1/auth/me");
        var viBody = await Envelope.ReadAsync(vi, HttpStatusCode.OK);
        var enBody = await Envelope.ReadAsync(en, HttpStatusCode.OK);

        RoleName(viBody).ShouldBe("Quản trị");
        RoleName(enBody).ShouldBe("Administrator");
        viBody.GetProperty("messageText").GetString().ShouldNotBe(enBody.GetProperty("messageText").GetString());

        // no preferred locale stored → "vi"
        using var guest = await (await fx.ClientAs(BetaTenant.GuestEmail)).GetAsync("/api/v1/auth/me");
        (await Envelope.ReadAsync(guest, HttpStatusCode.OK)).GetProperty("data").GetProperty("user").GetProperty("preferredLocale").GetString().ShouldBe("vi");

        static string? RoleName(JsonElement body) => body.GetProperty("data").GetProperty("user").GetProperty("roles")[0].GetProperty("name").GetString();
    }

    [Fact]
    public async Task Me_without_or_with_invalid_token_returns_401_envelope()
    {
        using var none = await fx.Client().GetAsync("/api/v1/auth/me");
        await Envelope.ReadAsync(none, HttpStatusCode.Unauthorized);

        var client = fx.Client();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "eyJhbGciOiJIUzI1NiJ9.e30.invalid");
        using var garbage = await client.GetAsync("/api/v1/auth/me");
        await Envelope.ReadAsync(garbage, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_returns_401_when_the_user_was_disabled_after_sign_in()
    {
        var client = await fx.ClientAs(BetaTenant.GuestEmail);
        try
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.users SET is_active = false WHERE id = '{BetaTenant.GuestUserId}'");
            using var response = await client.GetAsync("/api/v1/auth/me");
            await Envelope.ReadAsync(response, HttpStatusCode.Unauthorized);
        }
        finally
        {
            await fx.ExecuteOwnerAsync($"UPDATE dx.users SET is_active = true WHERE id = '{BetaTenant.GuestUserId}'");
        }
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<string> LoginCookieAsync(string email)
    {
        using var response = await fx.Client().PostAsJsonAsync("/api/v1/auth/login", new { email, password = BetaTenant.Password });
        await Envelope.ReadAsync(response, HttpStatusCode.OK);
        return SetCookie(response).ShouldNotBeNull().Value.Value!;
    }

    private async Task<HttpResponseMessage> RefreshAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"{Cookie}={token}");
        return await fx.Client().SendAsync(request);
    }

    private static SetCookieHeaderValue? SetCookie(HttpResponseMessage response)
        => response.Headers.TryGetValues(HeaderNames.SetCookie, out var values)
            ? SetCookieHeaderValue.ParseList(values.ToList()).FirstOrDefault(c => c.Name == Cookie)
            : null;

    private static bool IsDeletion(SetCookieHeaderValue? cookie)
        => cookie is not null && cookie.Value.Value == string.Empty && cookie.Expires < DateTimeOffset.UtcNow;

    private static string Hash(string token) => Dx.Application.Auth.AuthService.HashToken(token);

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToArray();
}
