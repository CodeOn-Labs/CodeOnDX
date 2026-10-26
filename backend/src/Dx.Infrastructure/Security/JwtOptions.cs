namespace Dx.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "dx.codeonlabs.vn";
    public string Audience { get; set; } = "dx-spa";

    /// <summary>HMAC-SHA256 key, at least 32 bytes. Only in appsettings.Secrets.json / environment (never in git).</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
}

/// <summary>Claim names in the access token.</summary>
public static class DxClaims
{
    public const string Subject = "sub";
    public const string Tenant = "tid";
    public const string Employee = "emp";
    public const string Permission = "perm";
    public const string Email = "email";
    public const string Name = "name";
}
