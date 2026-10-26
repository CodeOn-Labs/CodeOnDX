using System.Linq.Expressions;
using Dx.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dx.Infrastructure.Persistence;

/// <summary>
/// Hand-written part of the scaffolded DxDbContext (DB-first: DxDbContext.cs and the entities
/// are regenerated from the database, this file is not).
///   - global filters "tenant" (D1) and "soft_delete"
///   - app.tenant_id on every opened connection (PostgreSQL RLS, see V011)
///   - audit columns created_* / updated_* and tenant_id filled on SaveChanges
/// </summary>
public partial class DxDbContext : IAppDbContext
{
    /// <summary>Tables read before the tenant is known — same exclusions as the RLS policies (V011).</summary>
    private static readonly HashSet<string> TenantFilterExclusions = ["tenant_users", "refresh_tokens", "account_tokens"];

    private readonly ITenantContext? _tenant;
    private readonly ICurrentUser? _currentUser;

    [ActivatorUtilitiesConstructor]
    public DxDbContext(DbContextOptions<DxDbContext> options, ITenantContext tenant, ICurrentUser currentUser)
        : base(options)
    {
        _tenant = tenant;
        _currentUser = currentUser;
    }

    /// <summary>Evaluated per query by the tenant filter; Guid.Empty matches nothing.</summary>
    public Guid CurrentTenantId => _tenant?.TenantId ?? Guid.Empty;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.AddInterceptors(new TenantConnectionInterceptor(this));

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            var builder = modelBuilder.Entity(clr);
            var e = Expression.Parameter(clr, "e");

            if (clr.GetProperty("TenantId")?.PropertyType == typeof(Guid) && !TenantFilterExclusions.Contains(entityType.GetTableName()!))
            {
                // e => e.TenantId == this.CurrentTenantId  (EF parameterizes the context member per query)
                var body = Expression.Equal(
                    Expression.Property(e, "TenantId"),
                    Expression.Property(Expression.Constant(this), nameof(CurrentTenantId)));
                builder.HasQueryFilter(QueryFilters.Tenant, Expression.Lambda(body, e));
            }

            if (clr.GetProperty("IsDeleted")?.PropertyType == typeof(bool))
            {
                // e => !e.IsDeleted
                builder.HasQueryFilter(QueryFilters.SoftDelete, Expression.Lambda(Expression.Not(Expression.Property(e, "IsDeleted")), e));
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampEntries();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampEntries()
    {
        var now = DateTime.UtcNow;
        Guid? userId = _currentUser is { IsAuthenticated: true } ? _currentUser.UserId : null;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            var props = entry.Metadata;

            if (entry.State == EntityState.Added)
            {
                if (props.FindProperty("TenantId") is { } tenantProp && tenantProp.ClrType == typeof(Guid)
                    && (Guid)entry.Property("TenantId").CurrentValue! == Guid.Empty)
                {
                    entry.Property("TenantId").CurrentValue = CurrentTenantId;
                }
                if (props.FindProperty("CreatedAt") is not null) entry.Property("CreatedAt").CurrentValue = now;
                if (props.FindProperty("CreatedBy") is not null && entry.Property("CreatedBy").CurrentValue is null) entry.Property("CreatedBy").CurrentValue = userId;
            }
            if (props.FindProperty("UpdatedAt") is not null) entry.Property("UpdatedAt").CurrentValue = now;
            if (props.FindProperty("UpdatedBy") is not null) entry.Property("UpdatedBy").CurrentValue = userId;
        }
    }
}
