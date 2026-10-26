using Dx.Api.Common;
using Dx.Application.Common;
using Dx.Infrastructure.Security;

namespace Dx.Api.Security;

/// <summary>
/// Per-request user + tenant, read from the access token claims. Login / refresh set the
/// tenant explicitly through ITenantContext.Use before any token exists.
/// </summary>
public sealed class RequestContext(IHttpContextAccessor http) : ICurrentUser, ITenantContext
{
    private Guid? _explicitTenant;
    private IReadOnlySet<string>? _permissions;

    private System.Security.Claims.ClaimsPrincipal? Principal => http.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => ClaimGuid(DxClaims.Subject) ?? throw new UnauthorizedException();

    public Guid TenantId => (this as ITenantContext).TenantId ?? throw new UnauthorizedException();

    Guid? ITenantContext.TenantId => _explicitTenant ?? (IsAuthenticated ? ClaimGuid(DxClaims.Tenant) : null);

    public void Use(Guid tenantId) => _explicitTenant = tenantId;

    public IReadOnlySet<string> Permissions =>
        _permissions ??= Principal?.FindAll(DxClaims.Permission).Select(c => c.Value).ToHashSet(StringComparer.Ordinal) ?? [];

    public string Culture => JsonStringLocalizer.CultureOf(http.HttpContext);

    private Guid? ClaimGuid(string type)
        => Guid.TryParse(Principal?.FindFirst(type)?.Value, out var id) ? id : null;
}
