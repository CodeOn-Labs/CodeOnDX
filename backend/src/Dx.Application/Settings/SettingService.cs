using Dx.Application.Common;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Settings;

/// <summary>General tenant settings (Admin › Settings) as { key: "value" }.</summary>
public sealed class SettingService(IAppDbContext db)
{
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct)
        => await db.Set<TenantSetting>().AsNoTracking().OrderBy(s => s.Key).ToDictionaryAsync(s => s.Key, s => s.Value, ct);
}
