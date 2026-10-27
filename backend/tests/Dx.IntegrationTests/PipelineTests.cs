using System.Net;
using Dx.IntegrationTests.Infrastructure;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>Safety nets of the pipeline (Q2): every /api answer is an ErrorFullModel.</summary>
public sealed class PipelineTests(ApiFixture fx)
{
    [Fact]
    public async Task Health_reports_database_ok()
    {
        using var response = await fx.Client().GetAsync("/health");
        var data = (await Envelope.ReadAsync(response, HttpStatusCode.OK)).GetProperty("data");
        data.GetProperty("status").GetString().ShouldBe("ok");
        data.GetProperty("database").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Unknown_api_route_returns_404_envelope()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).GetAsync("/api/v1/does-not-exist");
        await Envelope.ReadAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Wrong_method_returns_405_envelope()
    {
        using var response = await (await fx.ClientAs(BetaTenant.AdminEmail)).DeleteAsync("/api/v1/settings");
        await Envelope.ReadAsync(response, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        using var response = await fx.Client().GetAsync("/health");
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["strict-origin-when-cross-origin"]);
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        csp.ShouldContain("script-src 'self';");
        csp.ShouldContain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task Messages_are_vietnamese_by_default_and_english_on_request()
    {
        using var vi = await fx.Client().GetAsync("/api/v1/settings");
        using var en = await fx.Client("en").GetAsync("/api/v1/settings");
        var viText = (await Envelope.ReadAsync(vi, HttpStatusCode.Unauthorized)).GetProperty("messageText").GetString();
        var enText = (await Envelope.ReadAsync(en, HttpStatusCode.Unauthorized)).GetProperty("messageText").GetString();
        viText.ShouldNotBe(enText);
        viText!.ShouldNotContain("error.");
        enText!.ShouldNotContain("error.");
    }
}
