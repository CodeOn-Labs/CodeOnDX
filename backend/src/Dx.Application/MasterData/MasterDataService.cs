using System.Text.Json;
using Dx.Application.Common;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.MasterData;

public sealed record MasterDataDto(Guid Id, string Type, string Code, string Name, string? Color, int SortSeq, JsonElement? Metadata);

/// <summary>Reference to a lookup row embedded in other DTOs.</summary>
public sealed record MasterRefDto(Guid Id, string Code, string Name, string? Color);

public sealed class MasterDataService(IAppDbContext db, ICurrentUser user)
{
    /// <summary>GET /master-data?type=a,b — active rows, name in the request culture.</summary>
    public async Task<IReadOnlyList<MasterDataDto>> ListAsync(IReadOnlyCollection<string> types, CancellationToken ct)
    {
        var query = db.Set<MasterDatum>().AsNoTracking().Where(m => m.IsActive);
        if (types.Count > 0) query = query.Where(m => types.Contains(m.Type));

        var rows = await query.OrderBy(m => m.Type).ThenBy(m => m.SortOrder).ThenBy(m => m.Code).ToListAsync(ct);
        return rows.Select(m => new MasterDataDto(
            m.Id, m.Type, m.Code, Localized.Name(user.Culture, m.Name, m.NameEn),
            Localized.Color(m.Metadata), m.SortOrder, Localized.Json(m.Metadata))).ToList();
    }

    public static MasterRefDto? Ref(MasterDatum? m, string culture)
        => m is null ? null : new MasterRefDto(m.Id, m.Code, Localized.Name(culture, m.Name, m.NameEn), Localized.Color(m.Metadata));
}
