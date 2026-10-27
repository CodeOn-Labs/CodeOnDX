using System.Text.Json;
using Dx.Application.Common;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Settings;

public sealed record SystemRuleDto(
    string Key, string Module, string ValueType, JsonElement? Value, JsonElement? DefaultValue,
    decimal? Min, decimal? Max, int Status, string Name, string? Description, DateTime? UpdatedAt);

public sealed record UpdateSystemRuleRequest(JsonElement? Value, int? Status);

/// <summary>
/// Business rules (Admin › Rules). Definitions are global (system_rules); each tenant
/// overrides the value in tenant_rules. No tenant row, or is_enabled = false, means the
/// default value applies (D12: formulas in code, parameters here).
/// </summary>
public sealed class RuleService(IAppDbContext db, ICurrentUser user)
{
    public async Task<IReadOnlyList<SystemRuleDto>> ListAsync(CancellationToken ct)
    {
        var defs = await db.Set<SystemRule>().AsNoTracking().OrderBy(r => r.SortOrder).ToListAsync(ct);
        var values = await db.Set<TenantRule>().AsNoTracking().ToDictionaryAsync(v => v.RuleKey, ct);
        return defs.Select(d => ToDto(d, values.GetValueOrDefault(d.Key))).ToList();
    }

    /// <summary>Numeric value of a rule for the current tenant, falling back to the default.</summary>
    public async Task<decimal> GetNumberAsync(string key, decimal fallback, CancellationToken ct)
    {
        var tenantValue = await db.Set<TenantRule>().AsNoTracking()
            .Where(v => v.RuleKey == key && v.IsEnabled).Select(v => v.Value).FirstOrDefaultAsync(ct);
        var json = tenantValue ?? await db.Set<SystemRule>().AsNoTracking()
            .Where(r => r.Key == key).Select(r => r.DefaultValue).FirstOrDefaultAsync(ct);
        return TryNumber(json, out var number) ? number : fallback;
    }

    /// <summary>Every rule of the tenant in one read, for code that checks many rules (timesheets, leave, attendance).</summary>
    public async Task<RuleSet> SnapshotAsync(CancellationToken ct)
    {
        var defs = await db.Set<SystemRule>().AsNoTracking().ToDictionaryAsync(r => r.Key, r => r.DefaultValue, ct);
        var values = await db.Set<TenantRule>().AsNoTracking().ToDictionaryAsync(v => v.RuleKey, v => (v.Value, v.IsEnabled), ct);
        return new RuleSet(defs, values);
    }

    /// <summary>PUT /system-rules/{key} — checks the value type and the min/max of the definition.</summary>
    public async Task<SystemRuleDto> UpdateAsync(string key, UpdateSystemRuleRequest request, CancellationToken ct)
    {
        var def = await db.Set<SystemRule>().AsNoTracking().FirstOrDefaultAsync(r => r.Key == key, ct)
                  ?? throw new NotFoundException();

        var errors = new ValidationErrors();
        string? valueJson = null;
        if (request.Value is not { } value || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            errors.Add("value", "validation.required");
        }
        else if (def.ValueType == "number")
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var n)) errors.Add("value", "validation.invalid_number");
            else if (def.MinValue is { } min && n < min) errors.Add("value", "validation.min_value", new() { ["min"] = min });
            else if (def.MaxValue is { } max && n > max) errors.Add("value", "validation.max_value", new() { ["max"] = max });
            else valueJson = value.GetRawText();
        }
        else if (def.ValueType == "boolean")
        {
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) errors.Add("value", "validation.invalid_value");
            else valueJson = value.GetRawText();
        }
        else
        {
            if (value.ValueKind != JsonValueKind.String) errors.Add("value", "validation.invalid_value");
            else valueJson = value.GetRawText();
        }
        if (request.Status is { } s && s is not (0 or 1)) errors.Add("status", "validation.invalid_value");
        errors.ThrowIfAny();

        var row = await db.Set<TenantRule>().FirstOrDefaultAsync(v => v.RuleKey == key, ct);
        if (row is null)
        {
            row = new TenantRule { TenantId = user.TenantId, RuleKey = key, Value = valueJson!, IsEnabled = true };
            db.Set<TenantRule>().Add(row);
        }
        row.Value = valueJson!;
        if (request.Status is { } status) row.IsEnabled = status == 1;
        await db.SaveChangesAsync(ct);
        return ToDto(def, row);
    }

    private SystemRuleDto ToDto(SystemRule d, TenantRule? v) => new(
        d.Key, d.Module, d.ValueType,
        Localized.Json(v?.Value ?? d.DefaultValue), Localized.Json(d.DefaultValue),
        d.MinValue, d.MaxValue, v is null || v.IsEnabled ? 1 : 0,
        Localized.Name(user.Culture, d.Name, d.NameEn),
        user.Culture == "en" && !string.IsNullOrWhiteSpace(d.DescriptionEn) ? d.DescriptionEn : d.Description,
        v?.UpdatedAt);

    private static bool TryNumber(string? json, out decimal number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(json)) return false;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Number && doc.RootElement.TryGetDecimal(out number);
    }
}

/// <summary>
/// Rule values of one tenant. A rule is "enabled" unless the tenant switched it off (Admin › Rules,
/// status 0); a disabled rule's value falls back to the default, and checks guarded by
/// <see cref="Enabled"/> are skipped altogether.
/// </summary>
public sealed class RuleSet(IReadOnlyDictionary<string, string> defaults, IReadOnlyDictionary<string, (string Value, bool IsEnabled)> values)
{
    public bool Enabled(string key) => !values.TryGetValue(key, out var v) || v.IsEnabled;

    public decimal Number(string key, decimal fallback)
    {
        using var doc = JsonDocument.Parse(Raw(key) ?? "null");
        return doc.RootElement.ValueKind == JsonValueKind.Number && doc.RootElement.TryGetDecimal(out var n) ? n : fallback;
    }

    public bool Bool(string key, bool fallback)
    {
        using var doc = JsonDocument.Parse(Raw(key) ?? "null");
        return doc.RootElement.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => fallback };
    }

    private string? Raw(string key)
        => values.TryGetValue(key, out var v) && v.IsEnabled ? v.Value : defaults.GetValueOrDefault(key);
}
