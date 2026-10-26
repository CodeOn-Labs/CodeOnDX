using System.Text.Json;
using Dx.Application.Auth;
using Dx.Application.Common;
using Shouldly;

namespace Dx.UnitTests;

public sealed class LocalizedTests
{
    [Theory]
    [InlineData("en", "Tên", "Name", "Name")]
    [InlineData("vi", "Tên", "Name", "Tên")]
    [InlineData("en", "Tên", null, "Tên")]
    [InlineData("en", "Tên", "  ", "Tên")]
    public void Name_picks_english_only_when_present(string culture, string name, string? nameEn, string expected)
        => Localized.Name(culture, name, nameEn).ShouldBe(expected);

    [Theory]
    [InlineData("""{"color":"#fff"}""", "#fff")]
    [InlineData("""{"color":1}""", null)]
    [InlineData("""{}""", null)]
    [InlineData("""[]""", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Color_reads_metadata_color_string(string? metadata, string? expected)
        => Localized.Color(metadata).ShouldBe(expected);

    [Fact]
    public void Json_parses_jsonb_text_and_returns_null_for_blank()
    {
        Localized.Json(null).ShouldBeNull();
        Localized.Json(" ").ShouldBeNull();
        var e = Localized.Json("""{"coefficient":1.5}""").ShouldNotBeNull();
        e.GetProperty("coefficient").GetDecimal().ShouldBe(1.5m);
        Localized.Json("42")!.Value.ValueKind.ShouldBe(JsonValueKind.Number);
    }
}

public sealed class ValidationErrorsTests
{
    [Fact]
    public void Collects_several_errors_per_field_and_throws_once()
    {
        var errors = new ValidationErrors()
            .Add("email", "validation.required")
            .Add("email", "validation.invalid_email")
            .Add("name", "validation.max_length", new() { ["max"] = 200 });

        var ex = Should.Throw<AppValidationException>(errors.ThrowIfAny);
        ex.MessageKey.ShouldBe("error.validation_failed");
        ex.Errors["email"].Select(e => e.Key).ShouldBe(["validation.required", "validation.invalid_email"]);
        ex.Errors["name"].Single().Args!["max"].ShouldBe(200);
    }

    [Fact]
    public void Does_not_throw_without_errors()
    {
        var errors = new ValidationErrors();
        errors.Any.ShouldBeFalse();
        Should.NotThrow(errors.ThrowIfAny);
    }

    [Fact]
    public void Single_builds_one_field_error()
    {
        var ex = AppValidationException.Single("value", "validation.min_value", new() { ["min"] = 1 });
        ex.Errors.Keys.ShouldBe(["value"]);
        ex.Errors["value"].Single().Key.ShouldBe("validation.min_value");
    }
}

public sealed class RefreshTokenHashTests
{
    [Fact]
    public void Hash_is_lowercase_sha256_hex_and_deterministic()
    {
        var hash = AuthService.HashToken("abc");
        hash.ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        AuthService.HashToken("abc").ShouldBe(hash);
        AuthService.HashToken("abd").ShouldNotBe(hash);
    }
}
