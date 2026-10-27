using System.Text.Json;
using System.Text.RegularExpressions;
using Dx.Application.Common;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Settings;

/// <summary>General tenant settings (Admin › Settings) as { key: "value" }.</summary>
public sealed partial class SettingService(IAppDbContext db, ICurrentUser user)
{
    private static readonly string[] TimeZones = ["Asia/Ho_Chi_Minh", "Asia/Tokyo", "Asia/Singapore", "UTC"];
    private static readonly string[] WorkWeeks = ["MON-FRI", "MON-SAT", "MON-SAT_HALF"];
    private static readonly string[] Locales = ["vi", "en"];
    private static readonly string[] Booleans = ["true", "false"];

    /// <summary>company_logo is a data:image/… URL; ~300 KB of image once base64 encoded.</summary>
    private const int MaxLogoLength = 400_000;

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct)
        => await db.Set<TenantSetting>().AsNoTracking().OrderBy(s => s.Key).ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    /// <summary>Integer setting of the current tenant, or <paramref name="fallback"/> when missing / not a number.</summary>
    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct)
    {
        var value = await db.Set<TenantSetting>().AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);
        return int.TryParse(value, out var n) ? n : fallback;
    }

    /// <summary>
    /// PUT /settings — partial update: only the keys in the body change. Every key has its own
    /// rule; an unknown key is a field error. Values are stored as strings (tenant_settings.value).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> UpdateAsync(IReadOnlyDictionary<string, JsonElement> body, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        var currencies = await db.Set<MasterDatum>().AsNoTracking()
            .Where(m => m.Type == MasterDataTypes.Currency && m.IsActive).Select(m => m.Code).ToListAsync(ct);

        foreach (var (key, raw) in body)
        {
            var value = raw.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                JsonValueKind.String => raw.GetString()!.Trim(),
                _ => raw.GetRawText().Trim(),
            };
            if (!Validate(key, value, raw, errors, currencies))
            {
                errors.Add(key, "validation.invalid_value");
                continue;
            }
            changes[key] = value;
        }

        var current = await db.Set<TenantSetting>().ToListAsync(ct);
        var merged = current.ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);
        foreach (var (k, v) in changes) merged[k] = v;
        if (merged.TryGetValue("work_start_time", out var start) && merged.TryGetValue("work_end_time", out var end)
            && TimePattern().IsMatch(start) && TimePattern().IsMatch(end) && string.CompareOrdinal(end, start) <= 0)
        {
            errors.Add("work_end_time", "admin_setting_update.err_end_before_start");
        }
        errors.ThrowIfAny();

        foreach (var (key, value) in changes)
        {
            var row = current.FirstOrDefault(s => s.Key == key);
            if (row is null) db.Set<TenantSetting>().Add(new TenantSetting { TenantId = user.TenantId, Key = key, Value = value });
            else row.Value = value;
        }
        await db.SaveChangesAsync(ct);
        return merged.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
    }

    /// <summary>false = unknown key. Field errors of a known key are added to <paramref name="e"/>.</summary>
    private static bool Validate(string key, string v, JsonElement raw, ValidationErrors e, IReadOnlyCollection<string> currencies)
    {
        switch (key)
        {
            case "company_name":
                e.Required(key, v);
                e.MaxLength(key, v, 150);
                return true;
            case "company_logo":
                if (v.Length > 0 && (!v.StartsWith("data:image/", StringComparison.Ordinal) || v.Length > MaxLogoLength))
                    e.Add(key, "admin_setting_update.err_logo_invalid");
                return true;
            case "default_locale":
                OneOf(e, key, v, Locales);
                return true;
            case "time_zone":
                OneOf(e, key, v, TimeZones);
                return true;
            case "currency":
                OneOf(e, key, v, currencies);
                return true;
            case "work_week":
                OneOf(e, key, v, WorkWeeks);
                return true;
            case "fiscal_year_start_month":
                e.Number(key, raw, 1, 12, integer: true);
                return true;
            case "work_start_time" or "work_end_time":
                if (!TimePattern().IsMatch(v)) e.Add(key, "validation.invalid_time");
                return true;
            case "lunch_break_minutes":
                e.Number(key, raw, 0, 240, integer: true);
                return true;
            case "notification_email":
                if (e.Required(key, v) && !Text.IsEmail(v)) e.Add(key, "validation.invalid_email");
                return true;
            case "monthly_report_ja_enabled" or "allow_app_check_in":
                OneOf(e, key, v, Booleans);
                return true;
            case "password_min_length":
                e.Number(key, raw, 6, 64, integer: true);
                return true;
            case "invitation_expiry_hours":
                e.Number(key, raw, 1, 720, integer: true);
                return true;
            default:
                return false;
        }
    }

    private static void OneOf(ValidationErrors e, string key, string value, IEnumerable<string> allowed)
    {
        if (!allowed.Contains(value, StringComparer.Ordinal)) e.Add(key, "validation.invalid_value");
    }

    [GeneratedRegex("^([01][0-9]|2[0-3]):[0-5][0-9]$")]
    private static partial Regex TimePattern();
}
