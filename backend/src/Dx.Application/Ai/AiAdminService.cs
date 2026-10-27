using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Dx.Application.Common;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Dx.Application.Ai;

public sealed record InsightConfigDto(
    Guid Id, string Name, string ScreenKey, string TriggerCondition, IReadOnlyList<string> DataSources, IReadOnlyList<Guid> AllowedRoleIds,
    string DataPrompt, string Status, DateTime? UpdatedAt);

public sealed record SaveInsightConfigRequest(
    string? Name, string? ScreenKey, string? TriggerCondition, IReadOnlyList<string?>? DataSources, IReadOnlyList<string?>? AllowedRoleIds,
    string? DataPrompt, string? Status);

public sealed record InsightPreviewRequest(string? ScreenKey, string? DataPrompt);

public sealed record PromptTemplateDto(
    Guid Id, string Name, string Scope, IReadOnlyList<string> ScreenKeys, string Template, string? Description, string Status, int Version, DateTime? UpdatedAt);

public sealed record SavePromptTemplateRequest(
    string? Name, string? Scope, IReadOnlyList<string?>? ScreenKeys, string? Template, string? Description, string? Status);

public sealed record PromptPreviewRequest(string? Template);

public sealed record PromptPreviewDto(string Output, IReadOnlyList<string> Variables);

public sealed record IntegrationDto(
    string Key, string Category, bool Enabled, bool UseMock, IReadOnlyDictionary<string, string> Settings, bool HasSecret,
    DateTime? LastSyncAt, DateTime? LastTestAt, bool? LastTestOk);

public sealed record SaveIntegrationRequest(bool? Enabled, Dictionary<string, JsonElement>? Settings);

public sealed record IntegrationPreviewItemDto(string? Title, string? Subtitle, string? Url, string? UpdatedAt);

/// <summary>
/// Admin › AI Insight (what each screen asks the LLM), Admin › Prompt templates and Admin ›
/// Integrations (the 5 connectors of Q3). Connector secrets never leave the server: the API only
/// says whether one is configured (appsettings.Secrets.json Integrations:&lt;Key&gt;:&lt;secret&gt;, or stored encrypted).
/// </summary>
public sealed partial class AiAdminService(
    IAppDbContext db, ICurrentUser user, IScreenCatalog screens, IMockDataStore mockData, IEmailOutbox outbox, AiService ai,
    IConfiguration config, ITenantClock clock)
{
    /// <summary>When an insight shows (labels in the FE: enum.insight_condition.*).</summary>
    public static readonly string[] Conditions =
        ["always", "budget_anomaly", "fte_shortage", "fte_overload", "unassigned_positions", "missing_timesheet", "schedule_weekly_mon", "ai_smart_detect"];

    /// <summary>Data an insight may read (labels in the FE: enum.insight_data_source.*).</summary>
    public static readonly string[] DataSources =
    [
        "timesheets", "employees", "budget", "milestones", "pcv", "css", "defect", "leakage", "ee", "issues_tracker", "git", "skills",
        "documents", "risks", "changes", "issues", "cross_links", "monthly_report", "projects", "leave", "attendance",
    ];

    private static readonly string[] PromptScopes = ["insight", "chat", "translate"];
    private static readonly string[] PromptStatuses = ["draft", "active", "archived"];

    /// <summary>Connector catalog: non-secret settings shown on the screen, the secret's name, preview file in mock mode.</summary>
    private static readonly Dictionary<string, (string Category, string[] Settings, string Secret, string? PreviewFile)> Connectors = new()
    {
        ["Ai"] = ("ai", ["baseUrl", "model", "maxTokens"], "ApiKey", null),
        ["GitHub"] = ("source_control", ["organization"], "Token", "github/repos.json"),
        ["GitLab"] = ("source_control", ["baseUrl", "group"], "Token", "gitlab/projects.json"),
        ["GoogleDrive"] = ("file_storage", ["rootFolderId"], "ServiceAccountJson", "google-drive/files.json"),
        ["Email"] = ("communication", ["fromAddress", "fromName", "smtpHost", "smtpPort"], "SmtpPassword", null),
    };

    private static readonly Dictionary<string, Dictionary<string, string>> DefaultSettings = new()
    {
        ["Ai"] = new() { ["baseUrl"] = "https://api.openai.example.com/v1", ["model"] = "gpt-mini", ["maxTokens"] = "2000" },
        ["GitHub"] = new() { ["organization"] = "codeon-demo" },
        ["GitLab"] = new() { ["baseUrl"] = "https://gitlab.example.com", ["group"] = "codeon" },
        ["GoogleDrive"] = new() { ["rootFolderId"] = string.Empty },
        ["Email"] = new() { ["fromAddress"] = "no-reply@example.com", ["fromName"] = "DX Platform", ["smtpHost"] = "smtp.example.com", ["smtpPort"] = "587" },
    };

    // ─── insight configs ─────────────────────────────────────────────────────

    public async Task<PagedResult<InsightConfigDto>> InsightConfigsAsync(string? search, IReadOnlyCollection<string> screenKeys, IReadOnlyCollection<string> statuses, PageRequest paging, CancellationToken ct)
    {
        var rows = await db.Set<AiInsightConfig>().AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt).ToListAsync(ct);
        var items = rows.Select(ToDto)
            .Where(c => Text.Matches(search, c.Name, c.DataPrompt))
            .Where(c => screenKeys.Count == 0 || screenKeys.Contains(c.ScreenKey))
            .Where(c => statuses.Count == 0 || statuses.Contains(c.Status)).ToList();
        return Paging.Of(items, paging);
    }

    public async Task<InsightConfigDto> InsightConfigAsync(Guid id, CancellationToken ct)
        => ToDto(await db.Set<AiInsightConfig>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException());

    public async Task<InsightConfigDto> CreateInsightConfigAsync(SaveInsightConfigRequest r, CancellationToken ct)
    {
        var row = new AiInsightConfig { SortOrder = await db.Set<AiInsightConfig>().CountAsync(ct) + 1 };
        await ApplyAsync(row, r, ct);
        db.Set<AiInsightConfig>().Add(row);
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<InsightConfigDto> UpdateInsightConfigAsync(Guid id, SaveInsightConfigRequest r, CancellationToken ct)
    {
        var row = await db.Set<AiInsightConfig>().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException();
        await ApplyAsync(row, r, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<string> DeleteInsightConfigAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<AiInsightConfig>().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException();
        db.Set<AiInsightResult>().RemoveRange(await db.Set<AiInsightResult>().Where(x => x.ConfigId == id).ToListAsync(ct));
        db.Set<AiInsightConfig>().Remove(row);
        await db.SaveChangesAsync(ct);
        return row.Name;
    }

    /// <summary>POST /ai/insight-configs/preview — runs the prompt once without saving.</summary>
    public async Task<IReadOnlyList<InsightItemDto>> PreviewInsightAsync(InsightPreviewRequest r, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var prompt = Text.Clean(r.DataPrompt);
        var screen = Text.Clean(r.ScreenKey);
        if (prompt.Length == 0) errors.Add("dataPrompt", "ai_insight_preview.err_prompt_required");
        if (!screens.Exists(screen)) errors.Add("screenKey", "validation.invalid_value");
        errors.ThrowIfAny();
        return await ai.PreviewAsync(screen, prompt, ct);
    }

    private async Task ApplyAsync(AiInsightConfig row, SaveInsightConfigRequest r, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var name = Text.Clean(r.Name);
        if (errors.Required("name", name)) errors.MaxLength("name", name, 150);
        var screen = Text.Clean(r.ScreenKey);
        if (errors.Required("screenKey", screen) && !screens.Exists(screen)) errors.Add("screenKey", "validation.invalid_value");
        var condition = Text.OrNull(r.TriggerCondition) ?? "always";
        if (!Conditions.Contains(condition)) errors.Add("triggerCondition", "validation.invalid_value");
        var sources = (r.DataSources ?? []).Select(Text.Clean).ToList();
        if (sources.Count == 0) errors.Add("dataSources", "validation.required");
        else if (sources.Any(s => !DataSources.Contains(s))) errors.Add("dataSources", "validation.invalid_value");
        var roleIds = (r.AllowedRoleIds ?? []).Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).ToList();
        var known = (await db.Set<Role>().AsNoTracking().Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        if (roleIds.Any(id => !known.Contains(id))) errors.Add("allowedRoleIds", "validation.invalid_value");
        var prompt = Text.Clean(r.DataPrompt);
        if (errors.Required("dataPrompt", prompt)) errors.MaxLength("dataPrompt", prompt, 4000);
        var status = Text.OrNull(r.Status) ?? "active";
        if (status is not ("active" or "paused")) errors.Add("status", "validation.invalid_value");
        errors.ThrowIfAny();
        row.Name = name;
        row.ScreenKey = screen;
        row.TriggerCondition = condition;
        row.DataSources = sources;
        row.AllowedRoleIds = roleIds;
        row.DataPrompt = prompt;
        row.IsActive = status == "active";
    }

    private static InsightConfigDto ToDto(AiInsightConfig c) => new(
        c.Id, c.Name, c.ScreenKey, c.TriggerCondition, c.DataSources, c.AllowedRoleIds, c.DataPrompt, c.IsActive ? "active" : "paused", c.UpdatedAt);

    // ─── prompt templates ────────────────────────────────────────────────────

    public async Task<PagedResult<PromptTemplateDto>> PromptTemplatesAsync(string? search, IReadOnlyCollection<string> scopes, IReadOnlyCollection<string> statuses, PageRequest paging, CancellationToken ct)
    {
        var rows = await db.Set<AiPromptTemplate>().AsNoTracking().OrderBy(t => t.CreatedAt).ToListAsync(ct);
        var items = rows.Where(t => Text.Matches(search, t.Name, t.Description))
            .Where(t => scopes.Count == 0 || scopes.Contains(t.Scope))
            .Where(t => statuses.Count == 0 || statuses.Contains(t.Status))
            .Select(ToDto).ToList();
        return Paging.Of(items, paging);
    }

    public async Task<PromptTemplateDto> PromptTemplateAsync(Guid id, CancellationToken ct)
        => ToDto(await db.Set<AiPromptTemplate>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException());

    public async Task<PromptTemplateDto> CreatePromptTemplateAsync(SavePromptTemplateRequest r, CancellationToken ct)
    {
        var row = new AiPromptTemplate { Version = 1 };
        Apply(row, r);
        db.Set<AiPromptTemplate>().Add(row);
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    /// <summary>Every save bumps the version.</summary>
    public async Task<PromptTemplateDto> UpdatePromptTemplateAsync(Guid id, SavePromptTemplateRequest r, CancellationToken ct)
    {
        var row = await db.Set<AiPromptTemplate>().FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException();
        Apply(row, r);
        row.Version += 1;
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    /// <summary>The only active template of a scope cannot be deleted (409 prompt_template_delete.err_last_active).</summary>
    public async Task<string> DeletePromptTemplateAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<AiPromptTemplate>().FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException();
        if (row.Status == "active" && await db.Set<AiPromptTemplate>().CountAsync(t => t.Scope == row.Scope && t.Status == "active", ct) == 1)
            throw new ConflictException("prompt_template_delete.err_last_active");
        db.Set<AiPromptTemplate>().Remove(row);
        await db.SaveChangesAsync(ct);
        return row.Name;
    }

    /// <summary>POST /ai/prompt-templates/preview — the template rendered with a sample context (no LLM call).</summary>
    public async Task<PromptPreviewDto> PreviewPromptAsync(PromptPreviewRequest r, CancellationToken ct)
    {
        var template = r.Template ?? string.Empty;
        var errors = new ValidationErrors();
        if (errors.Required("template", template.Trim()) && TemplateError(template) is { } err) errors.Add("template", err);
        errors.ThrowIfAny();
        var context = await ai.SampleContextAsync("project_list", ct);
        return new PromptPreviewDto(AiService.Render(template, context), context.Select(p => p.Key).ToList());
    }

    private void Apply(AiPromptTemplate row, SavePromptTemplateRequest r)
    {
        var errors = new ValidationErrors();
        var name = Text.Clean(r.Name);
        if (errors.Required("name", name)) errors.MaxLength("name", name, 150);
        var scope = Text.Clean(r.Scope);
        if (!PromptScopes.Contains(scope)) errors.Add("scope", "validation.invalid_value");
        var status = Text.OrNull(r.Status) ?? "draft";
        if (!PromptStatuses.Contains(status)) errors.Add("status", "validation.invalid_value");
        var keys = (r.ScreenKeys ?? []).Select(Text.Clean).ToList();
        if (keys.Any(k => !screens.Exists(k))) errors.Add("screenKeys", "validation.invalid_value");
        var template = r.Template ?? string.Empty;
        if (errors.Required("template", template.Trim()))
        {
            errors.MaxLength("template", template, 20000);
            if (TemplateError(template) is { } err) errors.Add("template", err);
        }
        var description = Text.Clean(r.Description);
        errors.MaxLength("description", description, 500);
        errors.ThrowIfAny();
        (row.Name, row.Scope, row.Status, row.IsActive, row.ScreenKeys, row.Template, row.Description) =
            (name, scope, status, status == "active", keys, template, Text.OrNull(description));
    }

    /// <summary>"{{" and "}}" must pair up and no variable may be empty.</summary>
    public static string? TemplateError(string template)
    {
        if (OpenBraces().Count(template) != CloseBraces().Count(template)) return "prompt_template_save.err_unbalanced";
        return EmptyVariable().IsMatch(template) ? "prompt_template_save.err_empty_variable" : null;
    }

    private static PromptTemplateDto ToDto(AiPromptTemplate t)
        => new(t.Id, t.Name, t.Scope, t.ScreenKeys, t.Template, t.Description, t.Status, t.Version, t.UpdatedAt);

    // ─── integrations ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<IntegrationDto>> IntegrationsAsync(CancellationToken ct)
        => (await db.Set<TenantIntegration>().AsNoTracking().ToListAsync(ct))
            .Where(i => Connectors.ContainsKey(i.IntegrationKey))
            .OrderBy(i => Connectors.Keys.ToList().IndexOf(i.IntegrationKey)).Select(ToDto).ToList();

    /// <summary>PUT /integrations/{key} — enabled + non-secret settings (URLs must be https).</summary>
    public async Task<IntegrationDto> UpdateIntegrationAsync(string key, SaveIntegrationRequest r, CancellationToken ct)
    {
        var row = await IntegrationAsync(key, ct);
        var def = Connectors[row.IntegrationKey];
        var errors = new ValidationErrors();
        var settings = JsonNode.Parse(string.IsNullOrWhiteSpace(row.Settings) ? "{}" : row.Settings)!.AsObject();
        foreach (var (k, raw) in r.Settings ?? [])
        {
            if (!def.Settings.Contains(k)) { errors.Add(k, "validation.invalid_value"); continue; }
            var v = raw.ValueKind switch { JsonValueKind.String => raw.GetString()!.Trim(), JsonValueKind.Null => string.Empty, _ => raw.GetRawText() };
            errors.MaxLength(k, v, 300);
            if (k.EndsWith("Url", StringComparison.OrdinalIgnoreCase) && v.Length > 0 && !v.StartsWith("https://", StringComparison.Ordinal)) errors.Add(k, "integration_update.err_https");
            if (k == "fromAddress" && v.Length > 0 && !Text.IsEmail(v)) errors.Add(k, "validation.invalid_email");
            if (k is "smtpPort" or "maxTokens" && v.Length > 0 && !Digits().IsMatch(v)) errors.Add(k, "validation.invalid_number");
            settings[k] = v;
        }
        if (r.Enabled is null) errors.Add("enabled", "validation.invalid_value");
        errors.ThrowIfAny();
        row.IsEnabled = r.Enabled!.Value;
        row.Settings = settings.ToJsonString();
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    /// <summary>
    /// POST /integrations/{key}/test — mock mode always connects; real mode needs the secret on the
    /// server. The result is stored (lastTestAt / lastTestOk) even when it fails.
    /// </summary>
    public async Task<IntegrationDto> TestIntegrationAsync(string key, CancellationToken ct)
    {
        var row = await IntegrationAsync(key, ct);
        var ok = row.IsEnabled && (row.UseMock || HasSecret(row));
        row.LastTestAt = clock.UtcNow.UtcDateTime;
        row.LastTestOk = ok;
        await db.SaveChangesAsync(ct);
        if (!row.IsEnabled) throw new ConflictException("integration_test.err_disabled");
        if (!ok) throw new UpstreamException("integration_test.err_failed");
        return ToDto(row);
    }

    /// <summary>GET /integrations/{key}/preview — a few items the connector sees (mock data, or the mock e-mail outbox).</summary>
    public async Task<IReadOnlyList<IntegrationPreviewItemDto>> PreviewIntegrationAsync(string key, CancellationToken ct)
    {
        var row = await IntegrationAsync(key, ct);
        if (!row.IsEnabled) throw new ConflictException("integration_test.err_disabled");
        if (row.IntegrationKey == "Email")
            return outbox.Recent(20).Select(m => new IntegrationPreviewItemDto(m.To, m.Subject, null, m.SentAt.ToString("O"))).ToList();
        if (Connectors[row.IntegrationKey].PreviewFile is not { } file || mockData.Read(file) is not { ValueKind: JsonValueKind.Array } items) return [];
        row.LastSyncAt = clock.UtcNow.UtcDateTime;
        await db.SaveChangesAsync(ct);
        static string? S(JsonElement x, params string[] names)
            => names.Select(n => x.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null).FirstOrDefault(v => v is not null);
        return items.EnumerateArray().Select(x => new IntegrationPreviewItemDto(
            S(x, "full_name", "path_with_namespace", "name"), S(x, "default_branch", "mimeType") ?? string.Empty,
            S(x, "html_url", "web_url", "webViewLink"), S(x, "pushed_at", "last_activity_at", "modifiedTime"))).ToList();
    }

    private async Task<TenantIntegration> IntegrationAsync(string key, CancellationToken ct)
        => Connectors.ContainsKey(key)
            ? await db.Set<TenantIntegration>().FirstOrDefaultAsync(i => i.IntegrationKey == key, ct) ?? throw new NotFoundException()
            : throw new NotFoundException();

    private bool HasSecret(TenantIntegration i)
        => !string.IsNullOrEmpty(i.SecretsEncrypted) || !string.IsNullOrWhiteSpace(config[$"Integrations:{i.IntegrationKey}:{Connectors[i.IntegrationKey].Secret}"]);

    private IntegrationDto ToDto(TenantIntegration i)
    {
        var def = Connectors[i.IntegrationKey];
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(i.Settings) ? "{}" : i.Settings);
        var stored = doc.RootElement.ValueKind == JsonValueKind.Object
            ? doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText())
            : [];
        var settings = def.Settings.ToDictionary(k => k, k => stored.GetValueOrDefault(k) ?? DefaultSettings[i.IntegrationKey].GetValueOrDefault(k) ?? string.Empty);
        return new IntegrationDto(i.IntegrationKey, def.Category, i.IsEnabled, i.UseMock, settings, !i.UseMock && HasSecret(i), i.LastSyncAt, i.LastTestAt, i.LastTestOk);
    }

    [GeneratedRegex(@"\{\{")]
    private static partial Regex OpenBraces();

    [GeneratedRegex(@"\}\}")]
    private static partial Regex CloseBraces();

    [GeneratedRegex(@"\{\{\s*\}\}")]
    private static partial Regex EmptyVariable();

    [GeneratedRegex(@"^\d{1,6}$")]
    private static partial Regex Digits();
}
