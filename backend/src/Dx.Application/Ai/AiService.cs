using System.Text.Json;
using System.Text.RegularExpressions;
using Dx.Application.Common;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dx.Application.Ai;

public sealed record InsightItemDto(string Id, string ScreenKey, string Variant, string Title, string Content);

public sealed record InsightsDto(string ScreenKey, DateTime GeneratedAt, bool Cached, IReadOnlyList<InsightItemDto> Items);

public sealed record ChatGreetingDto(string Message);

public sealed record ChatRequest(string? Message, string? Module);

public sealed record ChatReplyDto(string Reply, string Module);

/// <summary>
/// AI Insight panel and Chat AI (D5). Mock mode (Integrations:Ai:UseMock = true) answers from
/// mock-data/ai: insights of the screen and of its parents (ADR-0001 chain), chat scenarios by
/// keyword. Real mode sends the active prompt template + the insight's data prompt to the LLM and
/// keeps each generated insight for AiOptions.InsightCacheMinutes (ai_insight_results).
/// </summary>
public sealed partial class AiService(
    IAppDbContext db, ICurrentUser user, IScreenCatalog screens, IMockDataStore mockData, IAiClient llm, IOptions<AiOptions> options,
    Settings.SettingService settings, ITenantClock clock)
{
    private bool UseMock => options.Value.UseMock;

    /// <summary>GET /ai/insights?screen=&amp;refresh= — refresh=true asks the LLM again instead of the cached text.</summary>
    public async Task<InsightsDto> InsightsAsync(string screen, bool refresh, CancellationToken ct)
    {
        if (!ScreenKeyPattern().IsMatch(screen) || !screens.Exists(screen)) throw AppValidationException.Single("screen", "validation.invalid_value");
        var now = clock.UtcNow.UtcDateTime;
        if (UseMock) return new InsightsDto(screen, now, !refresh, MockInsights(screens.InsightChain(screen)));

        var chain = screens.InsightChain(screen);
        var myRoles = await MyRoleIdsAsync(ct);
        var configs = (await db.Set<AiInsightConfig>().AsNoTracking().Where(c => c.IsActive && chain.Contains(c.ScreenKey)).OrderBy(c => c.SortOrder).ToListAsync(ct))
            .Where(c => c.AllowedRoleIds.Count == 0 || c.AllowedRoleIds.Any(myRoles.Contains))
            .OrderBy(c => chain.ToList().IndexOf(c.ScreenKey)).ToList();
        var culture = user.Culture;
        var items = new List<InsightItemDto>();
        var cached = !refresh;
        foreach (var c in configs)
        {
            var hit = refresh ? null : await db.Set<AiInsightResult>().AsNoTracking()
                .Where(x => x.ConfigId == c.Id && x.ScopeKey == screen && x.Locale == culture && (x.ExpiresAt == null || x.ExpiresAt > now))
                .OrderByDescending(x => x.GeneratedAt).FirstOrDefaultAsync(ct);
            string content;
            if (hit is not null) content = hit.Content;
            else
            {
                cached = false;
                content = await AskAsync("insight", c.ScreenKey, c.DataPrompt, ct);
                db.Set<AiInsightResult>().Add(new AiInsightResult
                {
                    ConfigId = c.Id, ScopeKey = screen, Locale = culture, Content = content, Model = options.Value.Model,
                    GeneratedAt = now, ExpiresAt = now.AddMinutes(options.Value.InsightCacheMinutes),
                });
            }
            items.Add(new InsightItemDto(c.Id.ToString(), c.ScreenKey, "blue", c.Name, content));
        }
        await db.SaveChangesAsync(ct);
        return new InsightsDto(screen, now, cached && items.Count > 0, items);
    }

    /// <summary>Admin preview of a data prompt (nothing stored).</summary>
    public async Task<IReadOnlyList<InsightItemDto>> PreviewAsync(string screen, string dataPrompt, CancellationToken ct)
    {
        if (UseMock)
        {
            var items = MockInsights([screen]);
            return items.Count > 0 ? items : MockInsights(["project_list"]).Take(1).ToList();
        }
        return [new InsightItemDto($"{screen}-preview", screen, "blue", screen, await AskAsync("insight", screen, dataPrompt, ct))];
    }

    public ChatGreetingDto Greeting()
    {
        if (UseMock) return new ChatGreetingDto(Localize(mockData.Read("ai/chat/scenarios.json") is { } f && f.TryGetProperty("greeting", out var g) ? g : null));
        return new ChatGreetingDto(user.Culture == "en" ? "I am your AI assistant. How can I help?" : "Tôi là trợ lý AI. Tôi có thể giúp gì cho bạn?");
    }

    /// <summary>POST /ai/chat — { message (≤ 2000), module }.</summary>
    public async Task<ChatReplyDto> ChatAsync(ChatRequest r, CancellationToken ct)
    {
        var message = Text.Clean(r.Message);
        if (message.Length == 0) throw AppValidationException.Single("message", "validation.required");
        if (message.Length > 2000) throw AppValidationException.Single("message", "validation.max_length", new() { ["max"] = 2000 });
        var module = Text.OrNull(r.Module) ?? "general";
        if (UseMock)
        {
            var file = mockData.Read("ai/chat/scenarios.json");
            if (file is not { } f) return new ChatReplyDto(string.Empty, module);
            var text = message.ToLowerInvariant();
            var hit = f.GetProperty("scenarios").EnumerateArray()
                .FirstOrDefault(s => s.GetProperty("keywords").EnumerateArray().Any(k => text.Contains(k.GetString()!.ToLowerInvariant(), StringComparison.Ordinal)));
            return new ChatReplyDto(Localize(hit.ValueKind == JsonValueKind.Object ? hit.GetProperty("reply") : f.GetProperty("default_reply")), module);
        }
        return new ChatReplyDto(await AskAsync("chat", module, message, ct), module);
    }

    // ─── prompt building ─────────────────────────────────────────────────────

    /// <summary>
    /// System prompt = the active template of the scope (one bound to the screen first), rendered
    /// with the context; user prompt = the insight's data prompt or the chat message.
    /// </summary>
    private async Task<string> AskAsync(string scope, string screen, string userPrompt, CancellationToken ct)
    {
        var templates = await db.Set<AiPromptTemplate>().AsNoTracking().Where(t => t.Scope == scope && t.Status == "active").ToListAsync(ct);
        var template = templates.FirstOrDefault(t => t.ScreenKeys.Contains(screen)) ?? templates.FirstOrDefault(t => t.ScreenKeys.Count == 0) ?? templates.FirstOrDefault();
        var context = await SampleContextAsync(screen, ct);
        var system = template is null ? $"You are the assistant of {context["company_name"]}. Answer in {context["locale_name"]}." : Render(template.Template, context);
        return await llm.CompleteAsync(system, userPrompt, ct);
    }

    /// <summary>Variables available to templates: company_name, locale_name, today, screen_key, context.projects / employees, user.full_name.</summary>
    public async Task<IReadOnlyDictionary<string, object?>> SampleContextAsync(string screen, CancellationToken ct)
    {
        var company = (await settings.GetAllAsync(ct)).GetValueOrDefault("company_name");
        var projects = await db.Set<Project>().AsNoTracking().Select(p => p.Status.Code).ToListAsync(ct);
        var userId = user.UserId;
        var me = await db.Set<User>().AsNoTracking().Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return new Dictionary<string, object?>
        {
            ["company_name"] = company ?? string.Empty,
            ["locale_name"] = user.Culture == "en" ? "English" : "tiếng Việt",
            ["today"] = (await clock.TodayAsync(ct)).ToString("yyyy-MM-dd"),
            ["screen_key"] = screen,
            ["context"] = new Dictionary<string, object?>
            {
                ["projects"] = new Dictionary<string, object?> { ["total"] = projects.Count, ["running"] = projects.Count(c => c == "RUNNING") },
                ["employees"] = new Dictionary<string, object?> { ["total"] = await db.Set<Employee>().CountAsync(ct) },
            },
            ["user"] = new Dictionary<string, object?> { ["full_name"] = me ?? string.Empty },
        };
    }

    /// <summary>Replaces {{name}} / {{a.b}} with context values (objects as JSON); unknown variables stay as written.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, object?> context)
        => Variable().Replace(template, m =>
        {
            object? value = context;
            foreach (var part in m.Groups[1].Value.Split('.'))
                value = value is IReadOnlyDictionary<string, object?> d && d.TryGetValue(part, out var v) ? v : null;
            return value switch
            {
                null => $"{{{{{m.Groups[1].Value}}}}}",
                string s => s,
                IReadOnlyDictionary<string, object?> o => JsonSerializer.Serialize(o),
                _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            };
        });

    // ─── helpers ─────────────────────────────────────────────────────────────

    private List<InsightItemDto> MockInsights(IReadOnlyList<string> chain)
        => chain.SelectMany(key =>
        {
            if (mockData.Read($"ai/insights/{key}.json") is not { } file || !file.TryGetProperty("insights", out var all)) return [];
            var list = all.TryGetProperty(user.Culture, out var byCulture) ? byCulture : all.TryGetProperty("vi", out var vi) ? vi : default;
            return list.ValueKind != JsonValueKind.Array ? []
                : list.EnumerateArray().Select((it, i) => new InsightItemDto($"{key}-{i}", key,
                    it.GetProperty("variant").GetString()!, it.GetProperty("title").GetString()!, it.GetProperty("content").GetString()!)).ToList();
        }).ToList();

    private string Localize(JsonElement? byCulture)
        => byCulture is { ValueKind: JsonValueKind.Object } o
            ? (o.TryGetProperty(user.Culture, out var c) ? c.GetString() : o.TryGetProperty("vi", out var vi) ? vi.GetString() : null) ?? string.Empty
            : string.Empty;

    private async Task<HashSet<Guid>> MyRoleIdsAsync(CancellationToken ct)
    {
        var userId = user.UserId;
        return (await db.Set<UserRole>().AsNoTracking().Where(r => r.UserId == userId).Select(r => r.RoleId).ToListAsync(ct)).ToHashSet();
    }

    [GeneratedRegex(@"\{\{\s*([a-zA-Z_][\w.]*)\s*\}\}")]
    private static partial Regex Variable();

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex ScreenKeyPattern();
}
