using System.Text.RegularExpressions;
using Dx.Application.Common;
using Dx.Application.MasterData;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Organization;

public sealed record ClientFilter(
    string? Search, IReadOnlyCollection<Guid> CountryIds, IReadOnlyCollection<Guid> IndustryIds, string? Status, bool Deleted);

public sealed record ClientDto(
    Guid Id, string Code, string Name, string? ShortName, MasterRefDto? Country, MasterRefDto? Industry,
    string? ContactName, string? ContactEmail, string? DefaultCurrency, bool IsActive, bool IsDeleted,
    int ProjectsCount, DateTime CreatedAt, string? CountryCode);

public sealed record ClientSummaryDto(int Total, int Active, int Inactive, int TotalProjects);

public sealed record SaveClientRequest(
    string? Code, string? Name, string? ShortName, string? CountryId, string? IndustryId,
    string? ContactName, string? ContactEmail, string? DefaultCurrency, bool? IsActive);

/// <summary>
/// Admin › Clients. Delete moves the client to the trash (soft delete); deleting it again from the
/// trash removes it for good, only when no project (even a deleted one) still points to it.
/// </summary>
public sealed partial class ClientService(IAppDbContext db, ICurrentUser user)
{
    /// <summary>GET /clients — by name; projectsCount counts projects not in the trash.</summary>
    public async Task<PagedResult<ClientDto>> SearchAsync(ClientFilter filter, PageRequest paging, CancellationToken ct)
        => Paging.Of(await FilteredAsync(filter, ct), paging);

    /// <summary>GET /clients/summary — KPI cards over the same filter.</summary>
    public async Task<ClientSummaryDto> SummaryAsync(ClientFilter filter, CancellationToken ct)
    {
        var rows = await FilteredAsync(filter, ct);
        return new ClientSummaryDto(rows.Count, rows.Count(c => c.IsActive), rows.Count(c => !c.IsActive), rows.Sum(c => c.ProjectsCount));
    }

    public async Task<ClientDto> CreateAsync(SaveClientRequest request, CancellationToken ct)
    {
        var row = new Client();
        await ApplyAsync(row, request, ct);
        db.Set<Client>().Add(row);
        await db.SaveChangesAsync(ct);
        return await GetAsync(row.Id, ct);
    }

    public async Task<ClientDto> UpdateAsync(Guid id, SaveClientRequest request, CancellationToken ct)
    {
        var row = await FindAsync(id, ct);
        await ApplyAsync(row, request, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(row.Id, ct);
    }

    /// <summary>DELETE /clients/{id} — first call: to the trash; from the trash: permanent.</summary>
    public async Task<(bool Permanent, string Name)> DeleteAsync(Guid id, CancellationToken ct)
    {
        var row = await FindAsync(id, ct);
        if (!row.IsDeleted)
        {
            row.IsDeleted = true;
            row.DeletedAt = DateTime.UtcNow;
            row.DeletedBy = user.UserId;
            await db.SaveChangesAsync(ct);
            return (false, row.Name);
        }
        var used = await db.Set<Project>().IgnoreQueryFilters([QueryFilters.SoftDelete]).AnyAsync(p => p.ClientId == row.Id, ct);
        if (used) throw new ConflictException("client_delete.err_in_use", new() { ["name"] = row.Name });
        db.Set<Client>().Remove(row);
        await db.SaveChangesAsync(ct);
        return (true, row.Name);
    }

    public async Task<ClientDto> RestoreAsync(Guid id, CancellationToken ct)
    {
        var row = await FindAsync(id, ct);
        row.IsDeleted = false;
        row.DeletedAt = null;
        row.DeletedBy = null;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            throw new ConflictException(); // another active client took the same code meanwhile
        }
        return await GetAsync(row.Id, ct);
    }

    private async Task<ClientDto> GetAsync(Guid id, CancellationToken ct)
        => (await LoadAsync(db.Set<Client>().IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(c => c.Id == id), ct)).Single();

    private async Task<Client> FindAsync(Guid id, CancellationToken ct)
        => await db.Set<Client>().IgnoreQueryFilters([QueryFilters.SoftDelete]).FirstOrDefaultAsync(c => c.Id == id, ct)
           ?? throw new NotFoundException();

    private async Task<List<ClientDto>> FilteredAsync(ClientFilter f, CancellationToken ct)
    {
        var query = db.Set<Client>().IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(c => c.IsDeleted == f.Deleted);
        if (f.CountryIds.Count > 0) query = query.Where(c => c.CountryId != null && f.CountryIds.Contains(c.CountryId.Value));
        if (f.IndustryIds.Count > 0) query = query.Where(c => c.IndustryId != null && f.IndustryIds.Contains(c.IndustryId.Value));
        if (f.Status == "active") query = query.Where(c => c.IsActive);
        else if (f.Status == "inactive") query = query.Where(c => !c.IsActive);

        var rows = await LoadAsync(query, ct);
        return rows.Where(c => Text.Matches(f.Search, c.Name, c.Code, c.ContactName, c.ContactEmail))
            .OrderBy(c => c.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: false))
            .ToList();
    }

    private async Task<List<ClientDto>> LoadAsync(IQueryable<Client> query, CancellationToken ct)
    {
        var rows = await query.Include(c => c.Country).Include(c => c.Industry).AsNoTracking().ToListAsync(ct);
        var ids = rows.Select(c => c.Id).ToList();
        var counts = await db.Set<Project>().AsNoTracking()
            .Where(p => p.ClientId != null && ids.Contains(p.ClientId.Value))
            .GroupBy(p => p.ClientId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var culture = user.Culture;
        return rows.Select(c => new ClientDto(
            c.Id, c.Code, c.Name, c.ShortName, MasterDataService.Ref(c.Country, culture), MasterDataService.Ref(c.Industry, culture),
            c.ContactName, c.ContactEmail, c.DefaultCurrencyCode, c.IsActive, c.IsDeleted, counts.GetValueOrDefault(c.Id), c.CreatedAt,
            c.Country?.Code)).ToList();
    }

    private async Task ApplyAsync(Client row, SaveClientRequest r, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var name = Text.Clean(r.Name);
        var code = Text.Clean(r.Code).ToUpperInvariant();
        var contactEmail = Text.Clean(r.ContactEmail);
        var currency = Text.Clean(r.DefaultCurrency);

        if (errors.Required("name", name))
        {
            errors.MaxLength("name", name, 200);
            var lower = name.ToLower();
            if (await db.Set<Client>().AnyAsync(c => c.Name.ToLower() == lower && c.Id != row.Id, ct)) errors.Add("name", "client_save.err_duplicate_name");
        }
        if (code.Length > 0 && !CodePattern().IsMatch(code)) errors.Add("code", "client_save.err_code_format");
        else if (code.Length > 0 && await db.Set<Client>().IgnoreQueryFilters([QueryFilters.SoftDelete]).AnyAsync(c => c.Code == code && c.Id != row.Id, ct))
            errors.Add("code", "client_save.err_duplicate_code");
        errors.MaxLength("contactName", Text.Clean(r.ContactName), 200);
        if (contactEmail.Length > 0 && !Text.IsEmail(contactEmail)) errors.Add("contactEmail", "validation.invalid_email");
        var countryId = await MasterIdAsync(errors, "countryId", r.CountryId, MasterDataTypes.Country, ct);
        var industryId = await MasterIdAsync(errors, "industryId", r.IndustryId, MasterDataTypes.Industry, ct);
        if (currency.Length > 0 && !await db.Set<MasterDatum>().AnyAsync(m => m.Type == MasterDataTypes.Currency && m.Code == currency, ct))
            errors.Add("defaultCurrency", "validation.invalid_value");
        errors.ThrowIfAny();

        row.Name = name;
        // No code given: keep the current one, else derive it from the name (letters / digits, 12 chars).
        row.Code = code.Length > 0 ? code
            : !string.IsNullOrEmpty(row.Code) ? row.Code
            : NotCodeChars().Replace(name, string.Empty).ToUpperInvariant() is { Length: > 0 } derived ? derived[..Math.Min(12, derived.Length)]
            : Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        row.ShortName = Text.OrNull(r.ShortName);
        row.CountryId = countryId;
        row.IndustryId = industryId;
        row.ContactName = Text.OrNull(r.ContactName);
        row.ContactEmail = Text.OrNull(contactEmail);
        row.DefaultCurrencyCode = Text.OrNull(currency);
        row.IsActive = r.IsActive != false;
    }

    /// <summary>Optional master_data id that must be of <paramref name="type"/>.</summary>
    private async Task<Guid?> MasterIdAsync(ValidationErrors errors, string field, string? raw, string type, CancellationToken ct)
    {
        var id = errors.Id(field, raw);
        if (id is { } v && !await db.Set<MasterDatum>().AnyAsync(m => m.Id == v && m.Type == type, ct))
        {
            errors.Add(field, "validation.invalid_value");
            return null;
        }
        return id;
    }

    [GeneratedRegex("^[A-Z0-9_]{1,30}$")]
    private static partial Regex CodePattern();

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex NotCodeChars();
}
