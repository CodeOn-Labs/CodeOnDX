using System.Security.Claims;
using System.Text;
using Dx.Application.Auth;
using Dx.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dx.Infrastructure.Security;

/// <summary>
/// Access token = JWT (HS256) valid AccessTokenMinutes, carrying the tenant and the permission
/// keys so [HasPermission] needs no database round trip. Permission changes apply at the next
/// refresh (at most 15 minutes later).
/// </summary>
public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider time) : IAccessTokenIssuer
{
    public AccessToken Issue(User user, Guid tenantId, Guid? employeeId, IReadOnlyCollection<string> permissions)
    {
        var o = options.Value;
        var now = time.GetUtcNow().UtcDateTime;
        var claims = new List<Claim>
        {
            new(DxClaims.Subject, user.Id.ToString()),
            new(DxClaims.Tenant, tenantId.ToString()),
            new(DxClaims.Email, user.Email),
            new(DxClaims.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (employeeId is { } emp) claims.Add(new Claim(DxClaims.Employee, emp.ToString()));
        claims.AddRange(permissions.Select(p => new Claim(DxClaims.Permission, p)));

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(o.AccessTokenMinutes),
            SigningCredentials = new SigningCredentials(SigningKey(o), SecurityAlgorithms.HmacSha256),
        });
        return new AccessToken(token, o.AccessTokenMinutes * 60);
    }

    public static SymmetricSecurityKey SigningKey(JwtOptions o) => new(Encoding.UTF8.GetBytes(o.SigningKey));
}
