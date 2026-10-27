using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.Application.Ai;
using Dx.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dx.Infrastructure.Integrations;

/// <summary>
/// Files of mock-data/ copied next to the API (Dx.Api.csproj) or, on a dev machine, the repo folder.
/// Folder: Integrations:MockDataPath (absolute, or relative to the app folder).
/// </summary>
public sealed class JsonMockDataStore(IConfiguration config, IHostEnvironment env) : IMockDataStore
{
    private readonly ConcurrentDictionary<string, JsonElement?> _cache = new();

    public JsonElement? Read(string relativePath) => _cache.GetOrAdd(relativePath, path =>
    {
        foreach (var root in Roots())
        {
            var full = Path.GetFullPath(Path.Combine(root, path));
            if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) continue;
            using var doc = JsonDocument.Parse(File.ReadAllText(full));
            return doc.RootElement.Clone();
        }
        return null;
    });

    private IEnumerable<string> Roots()
    {
        var configured = config["Integrations:MockDataPath"];
        if (!string.IsNullOrWhiteSpace(configured)) yield return Path.IsPathRooted(configured) ? configured : Path.Combine(AppContext.BaseDirectory, configured);
        yield return Path.Combine(AppContext.BaseDirectory, "mock-data");
        yield return Path.Combine(env.ContentRootPath, "..", "..", "..", "mock-data");
    }
}

/// <summary>shared/screens.json, copied next to the API (or read from the repo on a dev machine).</summary>
public sealed class ScreenCatalog : IScreenCatalog
{
    private readonly Dictionary<string, (string? Parent, bool Inherit)> _screens = new(StringComparer.Ordinal);

    public ScreenCatalog(IHostEnvironment env)
    {
        var path = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "shared", "screens.json"),
            Path.Combine(env.ContentRootPath, "..", "..", "..", "shared", "screens.json"),
        }.FirstOrDefault(File.Exists);
        if (path is null) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var p in doc.RootElement.EnumerateObject().Where(p => !p.Name.StartsWith('$') && p.Value.ValueKind == JsonValueKind.Object))
        {
            var parent = p.Value.TryGetProperty("parent", out var pa) && pa.ValueKind == JsonValueKind.String ? pa.GetString() : null;
            var inherit = !(p.Value.TryGetProperty("inherit_insights", out var ii) && ii.ValueKind == JsonValueKind.False);
            _screens[p.Name] = (parent, inherit);
        }
        Keys = _screens.Keys.ToList();
    }

    public IReadOnlyList<string> Keys { get; } = [];

    public bool Exists(string screenKey) => _screens.ContainsKey(screenKey);

    public IReadOnlyList<string> InsightChain(string screenKey)
    {
        var chain = new List<string> { screenKey };
        var current = _screens.GetValueOrDefault(screenKey);
        while (current.Parent is { } parent && current.Inherit && !chain.Contains(parent))
        {
            chain.Add(parent);
            current = _screens.GetValueOrDefault(parent);
        }
        return chain;
    }
}

/// <summary>
/// OpenAI-compatible chat completion (D5) over plain HTTP: POST {BaseUrl}/chat/completions with
/// the server-side key. Failures surface as 502 (UpstreamException) without leaking details.
/// </summary>
public sealed class OpenAiCompatibleClient(HttpClient http, IOptions<AiOptions> options, ILogger<OpenAiCompatibleClient> logger) : IAiClient
{
    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.BaseUrl) || string.IsNullOrWhiteSpace(o.ApiKey) || string.IsNullOrWhiteSpace(o.Model))
            throw new BusinessException("ai_chat_send.err_not_configured");
        using var request = new HttpRequestMessage(HttpMethod.Post, o.BaseUrl.TrimEnd('/') + "/chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model = o.Model,
                max_tokens = o.MaxTokens,
                messages = new[] { new { role = "system", content = system }, new { role = "user", content = user } },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);
        try
        {
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogError(ex, "LLM call failed");
            throw new UpstreamException("integration_test.err_failed");
        }
    }
}

/// <summary>Placeholder used while UseMock = true: the AI services answer from mock-data and never call it.</summary>
public sealed class MockAiClient : IAiClient
{
    public Task<string> CompleteAsync(string system, string user, CancellationToken ct)
        => Task.FromResult("(mock) " + user[..Math.Min(200, user.Length)]);
}
