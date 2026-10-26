using Dx.Domain.Entities;

namespace Dx.Application.Auth;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record TenantRefDto(Guid Id, string Code, string Name);

public sealed record RoleRefDto(Guid Id, string Code, string Name);

public sealed record UserDto(
    Guid Id, string Email, string FullName, string? AvatarUrl, string PreferredLocale,
    Guid? EmployeeId, TenantRefDto? Tenant, IReadOnlyList<RoleRefDto> Roles);

/// <summary>Body of login / refresh responses (the refresh token itself goes in the dx_rt cookie).</summary>
public sealed record AuthResponse(string AccessToken, int ExpiresIn, UserDto User, IReadOnlyList<string> Permissions);

public sealed record MeResponse(UserDto User, IReadOnlyList<string> Permissions);

/// <summary>Result of a successful login / refresh: response body + new refresh token for the cookie.</summary>
public sealed record AuthSession(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshExpiresAt);

public sealed record AccessToken(string Token, int ExpiresIn);

/// <summary>Issues the short-lived JWT access token (D2: 15 minutes).</summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(User user, Guid tenantId, Guid? employeeId, IReadOnlyCollection<string> permissions);
}

/// <summary>ASP.NET Core Identity password hash format (V3, PBKDF2) — same hashes as mock-data.</summary>
public interface IPasswordHasher
{
    bool Verify(User user, string password, out bool needsRehash);

    string Hash(User user, string password);
}

public sealed class AuthOptions
{
    public const string Section = "Auth";

    public int RefreshTokenDays { get; set; } = 7;
    public int MaxFailedAccessAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Failed logins per client IP inside the window before answering 429.</summary>
    public int IpMaxFailures { get; set; } = 5;
    public int IpWindowMinutes { get; set; } = 5;
}
