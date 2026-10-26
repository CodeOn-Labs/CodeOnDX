using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dx.Api.Common;

/// <summary>
/// Messages of the API from Locales/vi.json and Locales/en.json (Q1), keys
/// "&lt;group&gt;.&lt;key&gt;" such as "project_create.msg_success". Placeholders are named
/// {snake_case}. Missing English text falls back to Vietnamese; a missing key returns the key.
/// Singleton; the culture comes from the current request (Accept-Language).
/// </summary>
public sealed partial class JsonStringLocalizer
{
    public const string DefaultCulture = "vi";
    public static readonly string[] SupportedCultures = ["vi", "en"];

    private readonly Dictionary<string, Dictionary<string, string>> _messages = new();
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<JsonStringLocalizer> _logger;

    public JsonStringLocalizer(IWebHostEnvironment env, IHttpContextAccessor http, ILogger<JsonStringLocalizer> logger)
    {
        _http = http;
        _logger = logger;
        foreach (var culture in SupportedCultures)
        {
            var path = Path.Combine(env.ContentRootPath, "Locales", $"{culture}.json");
            if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, "Locales", $"{culture}.json");
            _messages[culture] = Flatten(File.ReadAllText(path));
        }
    }

    /// <summary>"en" when Accept-Language starts with en, otherwise "vi".</summary>
    public static string CultureOf(HttpContext? context)
    {
        var header = context?.Request.Headers.AcceptLanguage.ToString() ?? string.Empty;
        return header.TrimStart().StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : DefaultCulture;
    }

    public string Text(string key, IReadOnlyDictionary<string, object?>? args = null, string? culture = null)
    {
        culture ??= CultureOf(_http.HttpContext);
        if (!(_messages.TryGetValue(culture, out var map) && map.TryGetValue(key, out var template))
            && !_messages[DefaultCulture].TryGetValue(key, out template))
        {
            _logger.LogWarning("Missing BE locale key {Key}", key);
            return key;
        }
        if (args is null || args.Count == 0) return template;
        return Placeholder().Replace(template, m => args.TryGetValue(m.Groups[1].Value, out var v) ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty : m.Value);
    }

    private static Dictionary<string, string> Flatten(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        void Walk(JsonElement element, string prefix)
        {
            foreach (var p in element.EnumerateObject())
            {
                var key = prefix.Length == 0 ? p.Name : $"{prefix}.{p.Name}";
                if (p.Value.ValueKind == JsonValueKind.Object) Walk(p.Value, key);
                else if (p.Value.ValueKind == JsonValueKind.String) result[key] = p.Value.GetString()!;
            }
        }
        Walk(doc.RootElement, string.Empty);
        return result;
    }

    [GeneratedRegex(@"\{([a-z0-9_]+)\}")]
    private static partial Regex Placeholder();
}
