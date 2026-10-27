using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dx.Application.Common;
using Dx.Application.MasterData;
using Dx.Application.Organization;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dx.Application.Auth;

public sealed record ForgotPasswordRequest(string? Email);

public sealed record ResetPasswordRequest(string? Token, string? Password, string? ConfirmPassword);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? Password, string? ConfirmPassword);

/// <summary>kind = "invite" (create the account) or "reset" (forgot password).</summary>
public sealed record AccountTokenDto(string Kind, string Email, DateTime ExpiresAt, int PasswordMinLength);

public sealed record ProfileEmployeeDto(
    Guid Id, string Code, MasterRefDto? Position, MasterRefDto? Level, UnitRefDto? Department, UnitRefDto? Team,
    DateOnly? JoinedDate, DateOnly? LeftDate, bool IsActive, IReadOnlyList<string> Skills);

public sealed record ProfileProjectDto(Guid Id, string Code, string Name);

/// <summary>GET /profile — the /auth/me user plus HR data and the projects running today.</summary>
public sealed record ProfileDto(
    Guid Id, string Email, string FullName, string? AvatarUrl, string PreferredLocale, Guid? EmployeeId,
    TenantRefDto? Tenant, IReadOnlyList<RoleRefDto> Roles, string? Phone, ProfileEmployeeDto? Employee,
    IReadOnlyList<ProfileProjectDto> Projects, DateTime? LastLoginAt);

/// <summary>
/// Self-service account flows (D2, D4): forgot / reset password, accepting an invitation, own
/// profile and password. One-time links are stored as SHA-256 hashes in account_tokens.
/// Password policy: at least tenant setting password_min_length (default 8) characters with an
/// upper-case letter, a lower-case letter and a digit.
/// </summary>
public sealed partial class AccountService(
    IAppDbContext db,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IPasswordHasher hasher,
    AuthService auth,
    SettingService settings,
    ITenantClock clock,
    IEmailSender email,
    IOptions<EmailOptions> emailOptions,
    TimeProvider time)
{
    /// <summary>Lifetime of a forgot-password link.</summary>
    public const int ResetTokenMinutes = 30;

    public const string KindInvite = "invite";
    public const string KindReset = "reset";

    /// <summary>Role given to a person who accepts an invitation (roles.code), when the tenant has it.</summary>
    public const string DefaultRoleCode = "member";

    private const int MaxAvatarLength = 400_000;

    /// <summary>
    /// POST /auth/forgot-password — always the same answer, whether the e-mail exists or not, so
    /// the endpoint cannot be used to discover accounts. The link goes to the user's default tenant.
    /// </summary>
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var address = Text.Clean(request.Email).ToLowerInvariant();
        if (address.Length == 0) throw AppValidationException.Single("email", "validation.required");
        if (!Text.IsEmail(address)) throw AppValidationException.Single("email", "validation.invalid_email");

        var normalized = address.ToUpperInvariant();
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.NormalizedEmail == normalized && u.IsActive, ct);
        if (user is null) return;
        var membership = await db.Set<TenantUser>().AsNoTracking()
            .Where(m => m.UserId == user.Id && m.IsActive && m.Tenant.IsActive)
            .OrderByDescending(m => m.IsDefault).Select(m => new { m.TenantId }).FirstOrDefaultAsync(ct);
        if (membership is null) return;

        var token = NewToken();
        db.Set<AccountToken>().Add(new AccountToken
        {
            TenantId = membership.TenantId,
            Purpose = AccountTokenPurposes.PasswordReset,
            TokenHash = AuthService.HashToken(token),
            Email = user.Email,
            UserId = user.Id,
            ExpiresAt = time.GetUtcNow().UtcDateTime.AddMinutes(ResetTokenMinutes),
        });
        await db.SaveChangesAsync(ct);
        await email.SendAsync(new EmailMessage(user.Email, AccountTokenPurposes.PasswordReset, new Dictionary<string, string>
        {
            ["full_name"] = user.FullName, ["link"] = Link(token), ["minutes"] = ResetTokenMinutes.ToString(),
        }, user.PreferredLocale ?? currentUser.Culture), ct);
    }

    /// <summary>GET /auth/reset-password/{token} — what the page needs before asking for the new password.</summary>
    public async Task<AccountTokenDto> CheckTokenAsync(string token, CancellationToken ct)
    {
        var row = await FindTokenAsync(token, ct);
        return new AccountTokenDto(KindOf(row), row.Email, row.ExpiresAt, await PasswordMinLengthAsync(ct));
    }

    /// <summary>
    /// POST /auth/reset-password — sets the password. Forgot password: also signs out every
    /// session of the user. Invitation: creates the login account (or links an account that
    /// already exists in another tenant), joins the tenant with role "member" and links the employee.
    /// Returns the kind, which picks the success message.
    /// </summary>
    public async Task<string> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var row = await FindTokenAsync(Text.Clean(request.Token), ct);
        var errors = new ValidationErrors();
        await ValidatePasswordAsync(errors, request.Password ?? string.Empty, request.ConfirmPassword ?? string.Empty, ct);
        errors.ThrowIfAny();

        var now = time.GetUtcNow().UtcDateTime;
        var kind = KindOf(row);
        if (kind == KindReset)
        {
            var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == row.UserId, ct) ?? throw new GoneException("account_token.err_expired");
            SetPassword(user, request.Password!);
            var sessions = await db.Set<RefreshToken>().Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct);
            foreach (var s in sessions)
            {
                s.RevokedAt = now;
                s.RevokedReason = "password_reset";
            }
        }
        else
        {
            var employee = await db.Set<Employee>().FirstOrDefaultAsync(e => e.Id == row.EmployeeId, ct);
            if (employee is null || employee.UserId is not null) throw new GoneException("account_token.err_expired");
            await AcceptInvitationAsync(employee, row.TenantId, request.Password!, ct);
        }
        row.ConsumedAt = now;
        await db.SaveChangesAsync(ct);
        return kind;
    }

    // ─── own profile ─────────────────────────────────────────────────────────

    public async Task<ProfileDto> GetProfileAsync(CancellationToken ct)
    {
        var user = await db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct) ?? throw new UnauthorizedException();
        var (dto, _) = await auth.LoadProfileAsync(user, currentUser.TenantId, ct);
        var employee = await db.Set<Employee>().AsNoTracking()
            .Include(e => e.Position).Include(e => e.Level).Include(e => e.Status).Include(e => e.Department).Include(e => e.Team)
            .FirstOrDefaultAsync(e => e.UserId == user.Id, ct);

        var projects = new List<ProfileProjectDto>();
        if (employee is not null)
        {
            var today = await clock.TodayAsync(ct);
            projects = (await db.Set<ProjectAssignment>().AsNoTracking()
                    .Where(a => a.EmployeeId == employee.Id && a.StartDate <= today && a.EndDate >= today && !a.Project.IsDeleted)
                    .Select(a => new ProfileProjectDto(a.Project.Id, a.Project.Code, a.Project.Name)).ToListAsync(ct))
                .DistinctBy(p => p.Id).ToList();
        }
        var culture = currentUser.Culture;
        return new ProfileDto(
            dto.Id, dto.Email, dto.FullName, dto.AvatarUrl, dto.PreferredLocale, dto.EmployeeId, dto.Tenant, dto.Roles,
            employee?.Phone,
            employee is null ? null : new ProfileEmployeeDto(
                employee.Id, employee.Code, MasterDataService.Ref(employee.Position, culture), MasterDataService.Ref(employee.Level, culture),
                employee.Department is null ? null : new UnitRefDto(employee.Department.Id, employee.Department.Name),
                employee.Team is null ? null : new UnitRefDto(employee.Team.Id, employee.Team.Name),
                employee.HireDate, employee.TerminationDate, employee.Status.Code != MasterDataCodes.EmployeeStatusResigned, employee.Skills),
            projects, user.LastLoginAt);
    }

    /// <summary>
    /// PUT /profile — partial: only the fields present in the body change. fullName / phone /
    /// skills / avatar are copied to the employee record too. avatarUrl null or "" removes the photo.
    /// </summary>
    public async Task<ProfileDto> UpdateProfileAsync(IReadOnlyDictionary<string, JsonElement> body, CancellationToken ct)
    {
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct) ?? throw new UnauthorizedException();
        var employee = await db.Set<Employee>().FirstOrDefaultAsync(e => e.UserId == user.Id, ct);
        var errors = new ValidationErrors();

        var fullName = body.TryGetValue("fullName", out var fn) ? StringOf(fn) : user.FullName;
        if (errors.Required("fullName", fullName)) errors.MaxLength("fullName", fullName, 150);
        string? phone = body.TryGetValue("phone", out var ph) ? StringOf(ph) : null;
        if (!string.IsNullOrEmpty(phone) && !PhonePattern().IsMatch(phone)) errors.Add("phone", "validation.invalid_value");
        string? locale = body.TryGetValue("preferredLocale", out var lo) ? StringOf(lo) : null;
        if (locale is not null && locale is not ("vi" or "en")) errors.Add("preferredLocale", "validation.invalid_value");
        var hasAvatar = body.TryGetValue("avatarUrl", out var av);
        string? avatar = null;
        if (hasAvatar && av.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            avatar = av.ValueKind == JsonValueKind.String ? av.GetString() : null;
            if (avatar is null || (avatar.Length > 0 && (!avatar.StartsWith("data:image/", StringComparison.Ordinal) || avatar.Length > MaxAvatarLength)))
                errors.Add("avatarUrl", "profile_update.err_avatar_invalid");
        }
        List<string>? skills = body.TryGetValue("skills", out var sk) && sk.ValueKind == JsonValueKind.Array
            ? sk.EnumerateArray().Select(s => s.ValueKind == JsonValueKind.String ? s.GetString()!.Trim() : string.Empty).Where(s => s.Length > 0).Take(30).ToList()
            : null;
        errors.ThrowIfAny();

        user.FullName = fullName;
        if (locale is not null) user.PreferredLocale = locale;
        if (hasAvatar) user.AvatarUrl = string.IsNullOrEmpty(avatar) ? null : avatar;
        if (employee is not null)
        {
            employee.FullName = fullName;
            if (phone is not null) employee.Phone = phone.Length == 0 ? null : phone;
            if (skills is not null) employee.Skills = skills;
            if (hasAvatar) employee.AvatarUrl = user.AvatarUrl;
        }
        await db.SaveChangesAsync(ct);
        return await GetProfileAsync(ct);
    }

    /// <summary>POST /profile/change-password.</summary>
    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct) ?? throw new UnauthorizedException();
        var errors = new ValidationErrors();
        var current = request.CurrentPassword ?? string.Empty;
        var password = request.Password ?? string.Empty;
        if (current.Length == 0) errors.Add("currentPassword", "validation.required");
        else if (!hasher.Verify(user, current, out _)) errors.Add("currentPassword", "account_password.err_current_wrong");
        await ValidatePasswordAsync(errors, password, request.ConfirmPassword ?? string.Empty, ct);
        if (current.Length > 0 && password == current) errors.Add("password", "account_password.err_same_as_current");
        errors.ThrowIfAny();

        SetPassword(user, password);
        await db.SaveChangesAsync(ct);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private async Task AcceptInvitationAsync(Employee employee, Guid tenantId, string password, CancellationToken ct)
    {
        var normalized = employee.Email.ToUpperInvariant();
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
        var hasOtherTenant = user is not null && await db.Set<TenantUser>().AnyAsync(m => m.UserId == user.Id, ct);
        if (user is null)
        {
            var locale = (await settings.GetAllAsync(ct)).GetValueOrDefault("default_locale") ?? "vi";
            user = new User
            {
                Id = Guid.CreateVersion7(),
                UserName = employee.Email,
                NormalizedUserName = normalized,
                Email = employee.Email,
                NormalizedEmail = normalized,
                EmailConfirmed = true, // the link proved the address
                FullName = employee.FullName,
                PreferredLocale = locale,
                LockoutEnabled = true,
                IsActive = true,
            };
            db.Set<User>().Add(user);
        }
        SetPassword(user, password);
        if (!await db.Set<TenantUser>().AnyAsync(m => m.UserId == user.Id && m.TenantId == tenantId, ct))
            db.Set<TenantUser>().Add(new TenantUser { TenantId = tenantId, UserId = user.Id, IsDefault = !hasOtherTenant, IsActive = true });
        employee.UserId = user.Id;

        var role = await db.Set<Role>().AsNoTracking().FirstOrDefaultAsync(r => r.Code == DefaultRoleCode, ct);
        if (role is not null && !await db.Set<UserRole>().AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, ct))
            db.Set<UserRole>().Add(new UserRole { TenantId = tenantId, UserId = user.Id, RoleId = role.Id });
    }

    private void SetPassword(User user, string password)
    {
        user.PasswordHash = hasher.Hash(user, password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.MustChangePassword = false;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
    }

    /// <summary>Valid, unused, unexpired token; also switches the request to the token's tenant.</summary>
    private async Task<AccountToken> FindTokenAsync(string token, CancellationToken ct)
    {
        if (token.Length == 0) throw new NotFoundException("account_token.err_invalid");
        var hash = AuthService.HashToken(token);
        var row = await db.Set<AccountToken>().FirstOrDefaultAsync(t => t.TokenHash == hash, ct) ?? throw new NotFoundException("account_token.err_invalid");
        if (row.ConsumedAt is not null || row.ExpiresAt <= time.GetUtcNow().UtcDateTime) throw new GoneException("account_token.err_expired");
        tenant.Use(row.TenantId);
        return row;
    }

    private static string KindOf(AccountToken row) => row.Purpose == AccountTokenPurposes.Invitation ? KindInvite : KindReset;

    private Task<int> PasswordMinLengthAsync(CancellationToken ct) => settings.GetIntAsync("password_min_length", 8, ct);

    private async Task ValidatePasswordAsync(ValidationErrors errors, string password, string confirm, CancellationToken ct)
    {
        var min = await PasswordMinLengthAsync(ct);
        if (password.Length == 0) errors.Add("password", "validation.required");
        else
        {
            if (password.Length < min) errors.Add("password", "account_password.err_too_short", new() { ["min"] = min });
            if (!password.Any(char.IsAsciiLetterUpper) || !password.Any(char.IsAsciiLetterLower) || !password.Any(char.IsAsciiDigit))
                errors.Add("password", "account_password.err_weak");
        }
        if (confirm != password) errors.Add("confirmPassword", "account_password.err_mismatch");
    }

    private string Link(string token) => $"{emailOptions.Value.PublicBaseUrl.TrimEnd('/')}/auth/reset-password/{token}";

    private static string NewToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    private static string StringOf(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : string.Empty;

    [GeneratedRegex(@"^[0-9+\-\s]{6,20}$")]
    private static partial Regex PhonePattern();
}
