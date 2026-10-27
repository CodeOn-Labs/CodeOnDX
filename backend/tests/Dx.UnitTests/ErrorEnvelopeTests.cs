using Dx.Api.Common;
using Dx.Application.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Dx.UnitTests;

/// <summary>Q2: ApiControllerBase.HandleException maps every exception to ErrorFullModel, never leaking ex.Message.</summary>
public sealed class ErrorEnvelopeTests
{
    private sealed class ProbeController : ApiControllerBase
    {
        public IActionResult Fail(Exception ex) => HandleException(ex);

        public IActionResult Ok(object? data, string key, int status = 200) => Success(data, key, status);
    }

    private static ProbeController Controller(string? acceptLanguage = null)
    {
        var http = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        if (acceptLanguage is not null) http.Request.Headers.AcceptLanguage = acceptLanguage;

        var accessor = new HttpContextAccessor { HttpContext = http };
        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(AppContext.BaseDirectory);
        http.RequestServices = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(new JsonStringLocalizer(env, accessor, NullLogger<JsonStringLocalizer>.Instance))
            .BuildServiceProvider();
        return new ProbeController { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private static ErrorFullModel Body(IActionResult result, int expectedStatus)
    {
        var obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(expectedStatus);
        var body = obj.Value.ShouldBeOfType<ErrorFullModel>();
        body.Status.ShouldBe(expectedStatus);
        body.MessageText.ShouldNotBeNullOrWhiteSpace();
        return body;
    }

    public static TheoryData<Exception, int> Mappings => new()
    {
        { new UnauthorizedException(), 401 },
        { new ForbiddenException(), 403 },
        { new NotFoundException(), 404 },
        { new ConflictException(), 409 },
        { new DbUpdateConcurrencyException("db detail"), 409 },
        { new BusinessException("error.conflict", new { reason = "locked" }), 422 },
        { new TooManyRequestsException(), 429 },
        { new InvalidOperationException("secret connection string"), 500 },
    };

    [Theory]
    [MemberData(nameof(Mappings))]
    public void Exceptions_map_to_status_codes(Exception ex, int status)
    {
        var body = Body(Controller().Fail(ex), status);
        body.MessageText.ShouldNotContain("secret");
        body.MessageText.ShouldNotContain("db detail");
    }

    [Fact]
    public void Internal_error_returns_only_the_trace_id()
    {
        var body = Body(Controller("en").Fail(new Exception("boom at line 42")), 500);
        body.MessageText.ShouldBe("Something went wrong, please try again later");
        // Anonymous types of two assemblies never compare equal — compare the JSON the client receives.
        System.Text.Json.JsonSerializer.Serialize(body.Data).ShouldBe("""{"traceId":"trace-123"}""");
    }

    [Fact]
    public void Validation_errors_become_localized_field_messages()
    {
        var ex = new ValidationErrors()
            .Add("value", "validation.max_value", new() { ["max"] = 24 })
            .Add("email", "validation.required");
        var thrown = Should.Throw<AppValidationException>(ex.ThrowIfAny);

        var body = Body(Controller("en").Fail(thrown), 400);
        body.MessageText.ShouldBe("Invalid data");
        var data = body.Data.ShouldBeOfType<Dictionary<string, string[]>>();
        data["value"].ShouldBe(["Maximum value is 24"]);
        data["email"].ShouldBe(["This field is required"]);
    }

    [Fact]
    public void Business_exception_keeps_its_data_and_message_args()
    {
        var body = Body(Controller("en").Fail(new BusinessException("validation.min_value", new { week = 3 }, new() { ["min"] = 5 })), 422);
        body.MessageText.ShouldBe("Minimum value is 5");
        body.Data.ShouldBeEquivalentTo(new { week = 3 });
    }

    [Fact]
    public void Success_wraps_data_with_the_localized_message()
    {
        var vi = Body(Controller().Ok(new { id = 1 }, "auth_login.msg_success"), 200);
        var en = Body(Controller("en").Ok(null, "auth_login.msg_success", 201), 201);
        en.MessageText.ShouldBe("Signed in");
        vi.MessageText.ShouldNotBe(en.MessageText);
        vi.Data.ShouldBeEquivalentTo(new { id = 1 });
    }
}

public sealed class JsonStringLocalizerTests
{
    private static JsonStringLocalizer Localizer(out DefaultHttpContext http)
    {
        http = new DefaultHttpContext();
        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(Path.Combine(Path.GetTempPath(), "no-such-dir")); // falls back to the app folder
        return new JsonStringLocalizer(env, new HttpContextAccessor { HttpContext = http }, NullLogger<JsonStringLocalizer>.Instance);
    }

    [Theory]
    [InlineData(null, "vi")]
    [InlineData("", "vi")]
    [InlineData("en", "en")]
    [InlineData("en-GB,en;q=0.8", "en")]
    [InlineData("  EN-us", "en")]
    [InlineData("fr-FR,en;q=0.5", "vi")]
    [InlineData("vi-VN", "vi")]
    public void Culture_is_english_only_when_accept_language_starts_with_en(string? header, string expected)
    {
        var http = new DefaultHttpContext();
        if (header is not null) http.Request.Headers.AcceptLanguage = header;
        JsonStringLocalizer.CultureOf(http).ShouldBe(expected);
    }

    [Fact]
    public void Text_uses_the_request_culture_and_fills_placeholders()
    {
        var l = Localizer(out var http);
        var vi = l.Text("validation.max_length", new Dictionary<string, object?> { ["max"] = 200 });
        http.Request.Headers.AcceptLanguage = "en";
        var en = l.Text("validation.max_length", new Dictionary<string, object?> { ["max"] = 200 });

        en.ShouldBe("At most 200 characters");
        vi.ShouldContain("200");
        vi.ShouldNotBe(en);
    }

    [Fact]
    public void Explicit_culture_wins_and_unknown_placeholders_stay()
    {
        var l = Localizer(out _);
        l.Text("validation.min_value", new Dictionary<string, object?> { ["other"] = 1 }, "en").ShouldBe("Minimum value is {min}");
        l.Text("validation.min_value", new Dictionary<string, object?> { ["min"] = 1.5m }, "en").ShouldBe("Minimum value is 1.5");
    }

    [Fact]
    public void Missing_key_returns_the_key()
        => Localizer(out _).Text("nope.missing_key").ShouldBe("nope.missing_key");
}
