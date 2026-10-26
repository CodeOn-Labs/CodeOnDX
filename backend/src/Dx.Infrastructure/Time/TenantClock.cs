using System.Collections.Concurrent;
using Dx.Application.Common;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Infrastructure.Time;

/// <summary>"Today" in the tenant time zone (tenants.time_zone, IANA id such as Asia/Ho_Chi_Minh).</summary>
public sealed class TenantClock(IAppDbContext db, ITenantContext tenant, TimeProvider time) : ITenantClock
{
    private static readonly ConcurrentDictionary<Guid, TimeZoneInfo> Zones = new();

    public DateTimeOffset UtcNow => time.GetUtcNow();

    public async Task<DateOnly> TodayAsync(CancellationToken ct = default)
    {
        var zone = TimeZoneInfo.Utc;
        if (tenant.TenantId is { } id)
        {
            if (!Zones.TryGetValue(id, out zone!))
            {
                var tz = await db.Set<Tenant>().AsNoTracking().Where(t => t.Id == id).Select(t => t.TimeZone).FirstOrDefaultAsync(ct);
                zone = tz is not null && TimeZoneInfo.TryFindSystemTimeZoneById(tz, out var found) ? found : TimeZoneInfo.Utc;
                Zones[id] = zone;
            }
        }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
    }
}
