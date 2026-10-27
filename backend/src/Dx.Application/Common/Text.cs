using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dx.Application.Common;

/// <summary>String helpers shared by services (same behaviour as tools/mock-api/lib/validate.mjs).</summary>
public static partial class Text
{
    /// <summary>Lower case without Vietnamese accents: "Đặng Văn A" → "dang van a".</summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c switch { 'đ' => 'd', 'Đ' => 'D', _ => c });
        }
        return sb.ToString().ToLowerInvariant();
    }

    /// <summary>Accent / case insensitive "contains" over several fields; empty query matches everything.</summary>
    public static bool Matches(string? query, params string?[] fields)
    {
        var q = Fold(query).Trim();
        return q.Length == 0 || fields.Any(f => Fold(f).Contains(q, StringComparison.Ordinal));
    }

    /// <summary>Trimmed string, or "" when the value is missing.</summary>
    public static string Clean(string? value) => value?.Trim() ?? string.Empty;

    /// <summary>Trimmed string, or null when blank.</summary>
    public static string? OrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsEmail(string value) => EmailPattern().IsMatch(value);

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailPattern();
}

/// <summary>Field checks writing the same locale keys as the mock API (validation.*).</summary>
public static class Check
{
    public static bool Required(this ValidationErrors errors, string field, string? value)
    {
        if (!string.IsNullOrEmpty(value)) return true;
        errors.Add(field, "validation.required");
        return false;
    }

    public static void MaxLength(this ValidationErrors errors, string field, string? value, int max)
    {
        if (value is not null && value.Length > max) errors.Add(field, "validation.max_length", new() { ["max"] = max });
    }

    /// <summary>JSON number (or numeric string) within [min, max]; null when missing or not a number.</summary>
    public static decimal? Number(this ValidationErrors errors, string field, JsonElement? value, decimal? min, decimal? max, bool integer = false)
    {
        decimal n;
        if (value is { ValueKind: JsonValueKind.Number } num && num.TryGetDecimal(out var d)) n = d;
        else if (value is { ValueKind: JsonValueKind.String } s
                 && decimal.TryParse(s.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var p)) n = p;
        else
        {
            errors.Add(field, "validation.invalid_number");
            return null;
        }
        if (integer && n != decimal.Truncate(n)) errors.Add(field, "validation.integer");
        if (min is { } lo && n < lo) errors.Add(field, "validation.min_value", new() { ["min"] = lo });
        if (max is { } hi && n > hi) errors.Add(field, "validation.max_value", new() { ["max"] = hi });
        return n;
    }

    /// <summary>yyyy-MM-dd date; missing → required (unless optional).</summary>
    public static DateOnly? Date(this ValidationErrors errors, string field, string? value, bool optional = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (!optional) errors.Add(field, "validation.required");
            return null;
        }
        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        errors.Add(field, "validation.invalid_date");
        return null;
    }

    /// <summary>Optional Guid given as string; invalid text → invalid_value.</summary>
    public static Guid? Id(this ValidationErrors errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Guid.TryParse(value.Trim(), out var id)) return id;
        errors.Add(field, "validation.invalid_value");
        return null;
    }
}
