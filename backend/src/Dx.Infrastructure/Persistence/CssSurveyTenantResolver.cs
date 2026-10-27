using Dx.Application.Projects;
using Microsoft.EntityFrameworkCore;

namespace Dx.Infrastructure.Persistence;

/// <summary>
/// The public CSS survey page has no signed-in tenant; dx.find_css_survey_tenant_id (V011,
/// SECURITY DEFINER) looks the token up across tenants without opening RLS for anything else.
/// </summary>
public sealed class CssSurveyTenantResolver(DxDbContext db) : ICssSurveyTenantResolver
{
    public async Task<Guid?> FindTenantAsync(string token, CancellationToken ct)
        => await db.Database.SqlQuery<Guid?>($"SELECT dx.find_css_survey_tenant_id({token}) AS \"Value\"").FirstOrDefaultAsync(ct);
}
