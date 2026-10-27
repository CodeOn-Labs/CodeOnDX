using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Common;

/// <summary>
/// Data access used by services (simplified Clean Architecture: services query EF Core
/// directly through this interface instead of a repository layer).
/// Implemented by Dx.Infrastructure.Persistence.DxDbContext.
/// </summary>
public interface IAppDbContext
{
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Tenant of the current request. Normally read from the access token (claim "tid");
/// flows that run before a token exists (login, refresh, public CSS survey) call Use().
/// Drives the EF Core global filter and the PostgreSQL setting app.tenant_id (RLS).
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    void Use(Guid tenantId);
}

/// <summary>Signed-in user of the current request.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    Guid TenantId { get; }
    IReadOnlySet<string> Permissions { get; }

    /// <summary>"vi" or "en" from Accept-Language (vi is the default and fallback).</summary>
    string Culture { get; }

    bool Has(string permission) => Permissions.Contains(permission);
}

/// <summary>Clock in the tenant time zone (tenants.time_zone).</summary>
public interface ITenantClock
{
    DateTimeOffset UtcNow { get; }

    Task<DateOnly> TodayAsync(CancellationToken ct = default);

    /// <summary>Wall-clock date and time in the tenant time zone.</summary>
    Task<DateTime> LocalNowAsync(CancellationToken ct = default);
}
