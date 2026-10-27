using Dx.Application.Common;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Access;

public sealed record PermissionDto(string Key, string Group, string Name, int SortSeq);

public sealed record RoleDto(
    Guid Id, string Code, string Name, string? Description, bool IsSystem,
    IReadOnlyList<string> Permissions, int MemberCount, DateTime UpdatedAt);

public sealed record RoleSummaryDto(int Total, int System, int Custom, int AssignedUsers);

public sealed record RoleFilter(string? Search, IReadOnlyCollection<string> Permissions, IReadOnlyCollection<string> Types);

public sealed record SaveRoleRequest(string? Name, string? Description, IReadOnlyList<string?>? Permissions);

public sealed record RolePermissionsUpdate(string? RoleId, IReadOnlyList<string?>? Permissions);

public sealed record BulkRolePermissionsRequest(IReadOnlyList<RolePermissionsUpdate>? Updates);

public sealed record AssignRoleUserRequest(string? UserId);

public sealed record UserRefDto(Guid Id, string FullName, string Email, string? AvatarUrl, bool IsActive);

/// <summary>
/// Admin › Roles &amp; permissions (D3). Permission keys are a global catalog; roles belong to the
/// tenant. Authorization never looks at role names. Every change that removes permissions or
/// members must leave at least one active user holding admin.roles, otherwise nobody could
/// manage roles any more (409 role_save.err_last_admin).
/// </summary>
public sealed class RoleService(IAppDbContext db, ICurrentUser user, TimeProvider time)
{
    public const string TypeSystem = "system";
    public const string TypeCustom = "custom";

    /// <summary>GET /permissions — catalog ordered for the permission matrix.</summary>
    public async Task<IReadOnlyList<PermissionDto>> PermissionsAsync(CancellationToken ct)
    {
        var rows = await db.Set<Permission>().AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Key).ToListAsync(ct);
        return rows.Select(p => new PermissionDto(p.Key, p.GroupKey, Localized.Name(user.Culture, p.Name, p.NameEn), p.SortOrder)).ToList();
    }

    /// <summary>GET /roles — system roles first, then by name; permissions filter = role has all of them.</summary>
    public async Task<PagedResult<RoleDto>> SearchAsync(RoleFilter filter, PageRequest paging, CancellationToken ct)
        => Paging.Of(await FilteredAsync(filter, ct), paging);

    public async Task<RoleSummaryDto> SummaryAsync(RoleFilter filter, CancellationToken ct)
    {
        var rows = await FilteredAsync(filter, ct);
        return new RoleSummaryDto(rows.Count, rows.Count(r => r.IsSystem), rows.Count(r => !r.IsSystem), rows.Sum(r => r.MemberCount));
    }

    public async Task<RoleDto> GetAsync(Guid id, CancellationToken ct)
        => (await LoadAsync(db.Set<Role>().Where(r => r.Id == id), ct)).SingleOrDefault() ?? throw new NotFoundException();

    public async Task<RoleDto> CreateAsync(SaveRoleRequest request, CancellationToken ct)
    {
        var (name, description, permissionIds) = await ValidateAsync(request, null, ct);
        var role = new Role
        {
            // Custom roles get a generated code; system roles keep the code the application knows them by.
            Code = $"custom_{time.GetUtcNow().ToUnixTimeMilliseconds():x}",
            Name = name,
            Description = description,
            IsSystem = false,
        };
        db.Set<Role>().Add(role);
        foreach (var pid in permissionIds) role.RolePermissions.Add(new RolePermission { TenantId = user.TenantId, PermissionId = pid });
        await db.SaveChangesAsync(ct);
        return await GetAsync(role.Id, ct);
    }

    public async Task<RoleDto> UpdateAsync(Guid id, SaveRoleRequest request, CancellationToken ct)
    {
        var role = await db.Set<Role>().Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException();
        var (name, description, permissionIds) = await ValidateAsync(request, role, ct);
        await EnsureRoleAdminRemainsAsync(new Dictionary<Guid, IReadOnlyCollection<Guid>> { [role.Id] = permissionIds }, null, ct);

        SetPermissions(role, permissionIds);
        role.Name = name;
        if (!role.IsSystem) role.NameEn = null; // the English name only exists for built-in roles
        role.Description = description;
        await db.SaveChangesAsync(ct);
        return await GetAsync(role.Id, ct);
    }

    /// <summary>PUT /roles/permissions — permission matrix: several roles at once, all or nothing.</summary>
    public async Task<IReadOnlyList<RoleDto>> BulkUpdatePermissionsAsync(BulkRolePermissionsRequest request, CancellationToken ct)
    {
        var updates = request.Updates ?? [];
        var errors = new ValidationErrors();
        var catalog = await CatalogAsync(ct);
        var roles = await db.Set<Role>().Include(r => r.RolePermissions).ToDictionaryAsync(r => r.Id, ct);
        var planned = new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        foreach (var u in updates)
        {
            if (!Guid.TryParse(u.RoleId, out var roleId) || !roles.ContainsKey(roleId)) errors.Add("updates", "validation.invalid_value");
            else if (u.Permissions is not { Count: > 0 }) errors.Add(u.RoleId!, "role_save.err_no_permission");
            else if (u.Permissions.Any(p => p is null || !catalog.ContainsKey(p))) errors.Add(u.RoleId!, "validation.invalid_value");
            else planned[roleId] = u.Permissions.Select(p => catalog[p!]).Distinct().ToList();
        }
        errors.ThrowIfAny();
        await EnsureRoleAdminRemainsAsync(planned, null, ct);

        foreach (var (roleId, permissionIds) in planned)
        {
            SetPermissions(roles[roleId], permissionIds);
            roles[roleId].UpdatedAt = time.GetUtcNow().UtcDateTime; // SaveChanges stamps only modified rows
        }
        await db.SaveChangesAsync(ct);
        var ids = planned.Keys.ToList();
        var dtos = (await LoadAsync(db.Set<Role>().Where(r => ids.Contains(r.Id)), ct)).ToDictionary(r => r.Id);
        return ids.Select(i => dtos[i]).ToList();
    }

    /// <summary>DELETE /roles/{id} — system roles stay; returns the name and how many users lost the role.</summary>
    public async Task<(string Name, int Members)> DeleteAsync(Guid id, CancellationToken ct)
    {
        var role = await db.Set<Role>().FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException();
        if (role.IsSystem) throw new ConflictException("role_delete.err_system");
        await EnsureRoleAdminRemainsAsync(new Dictionary<Guid, IReadOnlyCollection<Guid>> { [role.Id] = [] }, null, ct);
        var members = await db.Set<UserRole>().CountAsync(ur => ur.RoleId == role.Id, ct);
        db.Set<Role>().Remove(role); // role_permissions / user_roles cascade
        await db.SaveChangesAsync(ct);
        return (role.Name, members);
    }

    // ─── role members ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<UserRefDto>> MembersAsync(Guid roleId, CancellationToken ct)
    {
        if (!await db.Set<Role>().AnyAsync(r => r.Id == roleId, ct)) throw new NotFoundException();
        return await TenantUsers().Where(x => x.User.UserRoles.Any(ur => ur.RoleId == roleId))
            .OrderBy(x => x.User.FullName)
            .Select(x => new UserRefDto(x.User.Id, x.User.FullName, x.User.Email, x.User.AvatarUrl, x.User.IsActive && x.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserRefDto>> AssignAsync(Guid roleId, AssignRoleUserRequest request, CancellationToken ct)
    {
        if (!await db.Set<Role>().AnyAsync(r => r.Id == roleId, ct)) throw new NotFoundException();
        if (!Guid.TryParse(request.UserId, out var userId) || !await TenantUsers().AnyAsync(x => x.UserId == userId, ct)) throw new NotFoundException();
        if (!await db.Set<UserRole>().AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct))
        {
            db.Set<UserRole>().Add(new UserRole { TenantId = user.TenantId, UserId = userId, RoleId = roleId });
            await db.SaveChangesAsync(ct);
        }
        return await MembersAsync(roleId, ct);
    }

    public async Task<IReadOnlyList<UserRefDto>> UnassignAsync(Guid roleId, Guid userId, CancellationToken ct)
    {
        if (!await db.Set<Role>().AnyAsync(r => r.Id == roleId, ct)) throw new NotFoundException();
        var row = await db.Set<UserRole>().FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);
        if (row is not null)
        {
            await EnsureRoleAdminRemainsAsync(null, (userId, roleId), ct);
            db.Set<UserRole>().Remove(row);
            await db.SaveChangesAsync(ct);
        }
        return await MembersAsync(roleId, ct);
    }

    /// <summary>GET /users?search= — login accounts of the tenant (role assignment picker).</summary>
    public async Task<IReadOnlyList<UserRefDto>> UsersAsync(string? search, CancellationToken ct)
    {
        var rows = await TenantUsers()
            .Select(x => new UserRefDto(x.User.Id, x.User.FullName, x.User.Email, x.User.AvatarUrl, x.User.IsActive && x.IsActive))
            .ToListAsync(ct);
        return rows.Where(u => Text.Matches(search, u.FullName, u.Email)).OrderBy(u => u.FullName, StringComparer.CurrentCulture).ToList();
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    /// <summary>Memberships of the current tenant (tenant_users has no tenant filter: it is read at login).</summary>
    private IQueryable<TenantUser> TenantUsers()
    {
        var tenantId = user.TenantId;
        return db.Set<TenantUser>().AsNoTracking().Where(x => x.TenantId == tenantId);
    }

    private async Task<Dictionary<string, Guid>> CatalogAsync(CancellationToken ct)
        => await db.Set<Permission>().AsNoTracking().ToDictionaryAsync(p => p.Key, p => p.Id, StringComparer.Ordinal, ct);

    private void SetPermissions(Role role, IReadOnlyCollection<Guid> permissionIds)
    {
        foreach (var rp in role.RolePermissions.Where(rp => !permissionIds.Contains(rp.PermissionId)).ToList()) role.RolePermissions.Remove(rp);
        foreach (var pid in permissionIds.Where(pid => role.RolePermissions.All(rp => rp.PermissionId != pid)))
            role.RolePermissions.Add(new RolePermission { TenantId = user.TenantId, PermissionId = pid });
    }

    /// <summary>
    /// Checks the state after a change: some active member of the tenant must still hold
    /// admin.roles. <paramref name="plannedPermissions"/> replaces the permission set of those
    /// roles; <paramref name="removedMembership"/> is a user-role link about to be removed.
    /// </summary>
    private async Task EnsureRoleAdminRemainsAsync(
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>? plannedPermissions, (Guid UserId, Guid RoleId)? removedMembership, CancellationToken ct)
    {
        var adminPermission = await db.Set<Permission>().AsNoTracking().Where(p => p.Key == Permissions.AdminRoles).Select(p => p.Id).SingleAsync(ct);
        var adminRoles = (await db.Set<RolePermission>().AsNoTracking().Where(rp => rp.PermissionId == adminPermission).Select(rp => rp.RoleId).ToListAsync(ct))
            .ToHashSet();
        foreach (var (roleId, permissionIds) in plannedPermissions ?? new Dictionary<Guid, IReadOnlyCollection<Guid>>())
        {
            if (permissionIds.Contains(adminPermission)) adminRoles.Add(roleId);
            else adminRoles.Remove(roleId);
        }

        var tenantId = user.TenantId;
        var holders = await db.Set<UserRole>().AsNoTracking()
            .Where(ur => adminRoles.Contains(ur.RoleId) && ur.User.IsActive
                         && db.Set<TenantUser>().Any(tu => tu.UserId == ur.UserId && tu.TenantId == tenantId && tu.IsActive))
            .Select(ur => new { ur.UserId, ur.RoleId }).ToListAsync(ct);
        if (removedMembership is { } removed) holders.RemoveAll(h => h.UserId == removed.UserId && h.RoleId == removed.RoleId);
        if (holders.Count == 0) throw new ConflictException("role_save.err_last_admin");
    }

    private async Task<(string Name, string? Description, IReadOnlyCollection<Guid> PermissionIds)> ValidateAsync(
        SaveRoleRequest r, Role? current, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var name = Text.Clean(r.Name);
        var description = Text.Clean(r.Description);
        var keys = (r.Permissions ?? []).Select(Text.Clean).ToList();
        if (errors.Required("name", name))
        {
            errors.MaxLength("name", name, 100);
            if (current is { IsSystem: true } && name != current.Name) errors.Add("name", "role_save.err_system_name_locked");
            var lower = name.ToLower();
            var currentId = current?.Id;
            if (await db.Set<Role>().AnyAsync(x => x.Id != currentId && x.Name.ToLower() == lower, ct)) errors.Add("name", "role_save.err_duplicate_name");
        }
        errors.MaxLength("description", description, 500);
        var catalog = await CatalogAsync(ct);
        if (keys.Count == 0) errors.Add("permissions", "role_save.err_no_permission");
        else if (keys.Any(k => !catalog.ContainsKey(k))) errors.Add("permissions", "validation.invalid_value");
        errors.ThrowIfAny();
        return (name, Text.OrNull(description), keys.Select(k => catalog[k]).Distinct().ToList());
    }

    private async Task<List<RoleDto>> FilteredAsync(RoleFilter f, CancellationToken ct)
    {
        var rows = await LoadAsync(db.Set<Role>(), ct);
        var culture = System.Globalization.CultureInfo.GetCultureInfo(user.Culture == "en" ? "en-US" : "vi-VN");
        return rows
            .Where(r => Text.Matches(f.Search, r.Name, r.Code))
            .Where(r => f.Permissions.All(p => r.Permissions.Contains(p)))
            .Where(r => f.Types.Count == 0 || f.Types.Contains(r.IsSystem ? TypeSystem : TypeCustom))
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name, StringComparer.Create(culture, ignoreCase: false))
            .ToList();
    }

    private async Task<List<RoleDto>> LoadAsync(IQueryable<Role> query, CancellationToken ct)
    {
        var roles = await query.AsNoTracking()
            .Select(r => new
            {
                r.Id, r.Code, r.Name, r.NameEn, r.Description, r.IsSystem, r.UpdatedAt,
                Permissions = r.RolePermissions.Select(rp => rp.Permission.Key).ToList(),
                Members = r.UserRoles.Count,
            })
            .ToListAsync(ct);
        return roles.Select(r => new RoleDto(
            r.Id, r.Code, Localized.Name(user.Culture, r.Name, r.NameEn), r.Description, r.IsSystem,
            r.Permissions.Order(StringComparer.Ordinal).ToList(), r.Members, r.UpdatedAt)).ToList();
    }
}
