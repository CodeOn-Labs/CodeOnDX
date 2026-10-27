using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dx.Application.Common;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dx.Application.Auth;

/// <summary>
/// Sign-in (D2): JWT access token in the body, rotating refresh token in an httpOnly cookie.
/// Only the SHA-256 hash of a refresh token is stored. Presenting an already rotated token
/// is treated as theft and revokes the whole token family.
/// </summary>
public sealed partial class AuthService(
    IAppDbContext db,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IPasswordHasher hasher,
    IAccessTokenIssuer tokens,
    LoginThrottle throttle,
    IOptions<AuthOptions> options,
    TimeProvider time)
{
    private AuthOptions Options => options.Value;

    public async Task<AuthSession> LoginAsync(LoginRequest request, string clientIp, string? userAgent, CancellationToken ct)
    {
        throttle.EnsureAllowed(clientIp);

        var email = (request.Email ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        var errors = new ValidationErrors();
        if (email.Length == 0) errors.Add("email", "validation.required");
        else if (email.Length > 256 || !EmailRegex().IsMatch(email)) errors.Add("email", "validation.invalid_email");
        if (password.Length == 0) errors.Add("password", "validation.required");
        errors.ThrowIfAny();

        var now = time.GetUtcNow();
        var normalized = email.ToUpperInvariant();
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);

        // A locked account answers like a wrong password so attackers learn nothing.
        var locked = user?.LockoutEnd is { } end && end > now.UtcDateTime;
        if (user is null || locked || !hasher.Verify(user, password, out var needsRehash))
        {
            throttle.RecordFailure(clientIp);
            if (user is not null && !locked) await RegisterFailureAsync(user, now, ct);
            throw new UnauthorizedException("auth_login.err_invalid_credentials");
        }
        if (!user.IsActive) throw new ForbiddenException("auth_login.err_account_disabled");

        var membership = await db.Set<TenantUser>()
            .Where(m => m.UserId == user.Id && m.IsActive && m.Tenant.IsActive)
            .OrderByDescending(m => m.IsDefault)
            .Select(m => new { m.TenantId })
            .FirstOrDefaultAsync(ct)
            ?? throw new ForbiddenException("auth_login.err_account_disabled");

        throttle.Reset(clientIp);
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = now.UtcDateTime;
        if (needsRehash) user.PasswordHash = hasher.Hash(user, password);

        var (session, _) = await CreateSessionAsync(user, membership.TenantId, Guid.NewGuid(), clientIp, userAgent, ct);
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<AuthSession> RefreshAsync(string? refreshToken, string clientIp, string? userAgent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(refreshToken)) throw new UnauthorizedException("auth_refresh.err_session_expired");

        var now = time.GetUtcNow().UtcDateTime;
        var hash = HashToken(refreshToken);
        var stored = await db.Set<RefreshToken>().Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                     ?? throw new UnauthorizedException("auth_refresh.err_session_expired");

        if (stored.RevokedAt is not null)
        {
            // Reuse of a rotated token: revoke every live token of the family.
            await RevokeFamilyAsync(stored.FamilyId, "reuse_detected", now, ct);
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException("auth_refresh.err_session_expired");
        }
        if (stored.ExpiresAt <= now || !stored.User.IsActive)
            throw new UnauthorizedException("auth_refresh.err_session_expired");

        var stillMember = await db.Set<TenantUser>()
            .AnyAsync(m => m.UserId == stored.UserId && m.TenantId == stored.TenantId && m.IsActive && m.Tenant.IsActive, ct);
        if (!stillMember) throw new UnauthorizedException("auth_refresh.err_session_expired");

        var (session, replacement) = await CreateSessionAsync(stored.User, stored.TenantId, stored.FamilyId, clientIp, userAgent, ct);
        stored.RevokedAt = now;
        stored.RevokedReason = "rotated";
        stored.ReplacedBy = replacement;
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(refreshToken)) return;
        var hash = HashToken(refreshToken);
        var stored = await db.Set<RefreshToken>().FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, ct);
        if (stored is null) return;
        stored.RevokedAt = time.GetUtcNow().UtcDateTime;
        stored.RevokedReason = "logout";
        await db.SaveChangesAsync(ct);
    }

    public async Task<MeResponse> MeAsync(CancellationToken ct)
    {
        var user = await db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUser.UserId && u.IsActive, ct)
                   ?? throw new UnauthorizedException();
        var (dto, permissions) = await LoadProfileAsync(user, currentUser.TenantId, ct);
        return new MeResponse(dto, permissions);
    }

    private async Task<(AuthSession Session, RefreshToken Stored)> CreateSessionAsync(
        User user, Guid tenantId, Guid familyId, string clientIp, string? userAgent, CancellationToken ct)
    {
        tenant.Use(tenantId);
        var (dto, permissions) = await LoadProfileAsync(user, tenantId, ct);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiresAt = time.GetUtcNow().AddDays(Options.RefreshTokenDays);
        var stored = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TenantId = tenantId,
            FamilyId = familyId,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = expiresAt.UtcDateTime,
            CreatedByIp = Truncate(clientIp, 64),
            UserAgent = Truncate(userAgent, 500),
        };
        db.Set<RefreshToken>().Add(stored);

        var access = tokens.Issue(user, tenantId, dto.EmployeeId, permissions);
        return (new AuthSession(new AuthResponse(access.Token, access.ExpiresIn, dto, permissions), refreshToken, expiresAt), stored);
    }

    /// <summary>User DTO + permission keys of the user in the tenant (tenant context must already be set).</summary>
    internal async Task<(UserDto User, IReadOnlyList<string> Permissions)> LoadProfileAsync(User user, Guid tenantId, CancellationToken ct)
    {
        var tenantRow = await db.Set<Tenant>().AsNoTracking().Where(t => t.Id == tenantId)
            .Select(t => new TenantRefDto(t.Id, t.Code, t.Name)).FirstOrDefaultAsync(ct);
        var employee = await db.Set<Employee>().AsNoTracking().Where(e => e.UserId == user.Id)
            .Select(e => new { e.Id, e.AvatarUrl }).FirstOrDefaultAsync(ct);
        var roles = await db.Set<UserRole>().AsNoTracking().Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role).OrderBy(r => r.Code).ToListAsync(ct);
        var roleIds = roles.Select(r => r.Id).ToList();
        var permissions = await db.Set<RolePermission>().AsNoTracking().Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Key).Distinct().OrderBy(k => k).ToListAsync(ct);

        var culture = currentUser.Culture;
        var dto = new UserDto(
            user.Id, user.Email, user.FullName, user.AvatarUrl ?? employee?.AvatarUrl, user.PreferredLocale ?? "vi",
            employee?.Id, tenantRow,
            roles.Select(r => new RoleRefDto(r.Id, r.Code, Localized.Name(culture, r.Name, r.NameEn))).ToList());
        return (dto, permissions);
    }

    private async Task RegisterFailureAsync(User user, DateTimeOffset now, CancellationToken ct)
    {
        user.AccessFailedCount += 1;
        if (user.LockoutEnabled && user.AccessFailedCount >= Options.MaxFailedAccessAttempts)
        {
            user.LockoutEnd = now.AddMinutes(Options.LockoutMinutes).UtcDateTime;
            user.AccessFailedCount = 0;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task RevokeFamilyAsync(Guid familyId, string reason, DateTime now, CancellationToken ct)
    {
        var live = await db.Set<RefreshToken>().Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in live)
        {
            t.RevokedAt = now;
            t.RevokedReason = reason;
        }
    }

    public static string HashToken(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();
}
