using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;

namespace Dx.UnitTests;

/// <summary>Q1: backend locale files — same keys, same {placeholders}, keys snake_case in 2 levels.</summary>
public sealed partial class LocaleFilesTests
{
    private static readonly string LocalesDir = Path.Combine(AppContext.BaseDirectory, "Locales");

    private static Dictionary<string, string> Load(string culture)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(LocalesDir, $"{culture}.json")));
        return doc.RootElement.EnumerateObject()
            .SelectMany(g => g.Value.EnumerateObject().Select(k => (Key: $"{g.Name}.{k.Name}", Value: k.Value.GetString()!)))
            .ToDictionary(x => x.Key, x => x.Value);
    }

    [Fact]
    public void Vi_and_en_have_the_same_keys()
    {
        var vi = Load("vi");
        var en = Load("en");
        vi.Keys.Except(en.Keys).ShouldBeEmpty("keys missing in en.json");
        en.Keys.Except(vi.Keys).ShouldBeEmpty("keys missing in vi.json");
    }

    [Fact]
    public void Placeholders_match_between_languages()
    {
        var vi = Load("vi");
        var en = Load("en");
        foreach (var (key, text) in vi)
        {
            Placeholders(en[key]).ShouldBe(Placeholders(text), $"placeholders of {key}");
        }
    }

    [Fact]
    public void Keys_are_snake_case_with_two_levels()
    {
        foreach (var key in Load("vi").Keys)
        {
            KeyFormat().IsMatch(key).ShouldBeTrue(key);
        }
    }

    private static string[] Placeholders(string text)
        => PlaceholderRegex().Matches(text).Select(m => m.Value).Distinct().Order().ToArray();

    [GeneratedRegex(@"\{[a-z0-9_]+\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"^[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*$")]
    private static partial Regex KeyFormat();
}
