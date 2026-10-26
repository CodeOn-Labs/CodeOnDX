using System.Text.Json;

namespace Dx.Application.Common;

public static class Localized
{
    /// <summary>English name when the culture is "en" and one exists, otherwise the Vietnamese name.</summary>
    public static string Name(string culture, string name, string? nameEn)
        => culture == "en" && !string.IsNullOrWhiteSpace(nameEn) ? nameEn : name;

    /// <summary>jsonb columns are scaffolded as string; parse them for DTOs.</summary>
    public static JsonElement? Json(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    /// <summary>metadata.color of a master_data row, if any.</summary>
    public static string? Color(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        using var doc = JsonDocument.Parse(metadataJson);
        return doc.RootElement.ValueKind == JsonValueKind.Object
               && doc.RootElement.TryGetProperty("color", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString()
            : null;
    }
}
