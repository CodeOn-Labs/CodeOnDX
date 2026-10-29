using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Dx.Application.Common;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.MasterData;

/// <summary>Row of Admin › Categories. Name / nameEn are raw (both are edited on the form).</summary>
public sealed record CategoryDto(
    Guid Id, string Type, string Code, string Name, string? NameEn, string? Description, string? Color,
    int SortSeq, JsonElement Metadata, bool IsActive, bool IsSystem, DateTime? UpdatedAt);

public sealed record SaveCategoryRequest(
    string? Type, string? Code, string? Name, string? NameEn, string? Description, string? Color,
    JsonElement? SortSeq, JsonElement? Metadata, bool? IsActive);

/// <summary>
/// Admin › Categories: tenant-editable lookups (master_data). Rows referenced by business tables
/// cannot be deleted (only deactivated); system rows (is_system, e.g. task types OT / LEAVE) never.
/// </summary>
public sealed partial class CategoryService(IAppDbContext db)
{
    /// <summary>Types edited on the screen, in the order of its type filter.</summary>
    public static readonly string[] Types =
    [
        MasterDataTypes.ProjectStatus, MasterDataTypes.ProjectLevel, MasterDataTypes.RiskLevel, MasterDataTypes.HrLevel,
        MasterDataTypes.JobPosition, MasterDataTypes.PerformanceLevel, MasterDataTypes.TaskType, MasterDataTypes.TechRequestType, MasterDataTypes.EmployeeStatus,
        MasterDataTypes.LeaveType, MasterDataTypes.LinkType, MasterDataTypes.Currency, MasterDataTypes.Country, MasterDataTypes.Industry,
    ];

    /// <summary>GET /categories?search=&amp;types=&amp;page=&amp;pageSize= — ordered by type (screen order), then sort order.</summary>
    public async Task<PagedResult<CategoryDto>> SearchAsync(string? search, IReadOnlyCollection<string> types, PageRequest paging, CancellationToken ct)
    {
        var query = db.Set<MasterDatum>().AsNoTracking().Where(m => Types.Contains(m.Type));
        if (types.Count > 0) query = query.Where(m => types.Contains(m.Type));
        var rows = await query.ToListAsync(ct);

        var q = Text.Clean(search).ToLowerInvariant();
        var items = rows
            .Where(m => q.Length == 0 || new[] { m.Name, m.NameEn, m.Code }.Any(v => v?.ToLowerInvariant().Contains(q) == true))
            .OrderBy(m => Array.IndexOf(Types, m.Type)).ThenBy(m => m.SortOrder).ThenBy(m => m.Code, StringComparer.Ordinal)
            .Select(ToDto).ToList();
        return Paging.Of(items, paging);
    }

    public async Task<CategoryDto> CreateAsync(SaveCategoryRequest request, CancellationToken ct)
    {
        var row = new MasterDatum();
        await ApplyAsync(row, request, isNew: true, ct);
        db.Set<MasterDatum>().Add(row);
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, SaveCategoryRequest request, CancellationToken ct)
    {
        var row = await db.Set<MasterDatum>().FirstOrDefaultAsync(m => m.Id == id && Types.Contains(m.Type), ct) ?? throw new NotFoundException();
        await ApplyAsync(row, request, isNew: false, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    /// <summary>DELETE /categories/{id} — returns the name for the success message.</summary>
    public async Task<string> DeleteAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<MasterDatum>().FirstOrDefaultAsync(m => m.Id == id && Types.Contains(m.Type), ct) ?? throw new NotFoundException();
        if (row.IsSystem) throw new ConflictException("category_delete.err_system");

        db.Set<MasterDatum>().Remove(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsForeignKeyViolation(ex))
        {
            throw new ConflictException("category_delete.err_in_use", new() { ["name"] = row.Name });
        }
        return row.Name;
    }

    private async Task ApplyAsync(MasterDatum row, SaveCategoryRequest r, bool isNew, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var type = Text.Clean(r.Type);
        var code = Text.Clean(r.Code).ToUpperInvariant();
        var name = Text.Clean(r.Name);
        var nameEn = Text.Clean(r.NameEn);
        var description = Text.Clean(r.Description);
        var color = Text.Clean(r.Color);
        var metadata = r.Metadata is { ValueKind: JsonValueKind.Object } m ? JsonNode.Parse(m.GetRawText())!.AsObject() : new JsonObject();

        if (errors.Required("type", type) && !Types.Contains(type)) errors.Add("type", "validation.invalid_value");
        if (!isNew && type.Length > 0 && type != row.Type) errors.Add("type", "category_save.err_type_locked");
        if (errors.Required("code", code))
        {
            if (!CodePattern().IsMatch(code)) errors.Add("code", "category_save.err_code_format");
            else if (await db.Set<MasterDatum>().AnyAsync(x => x.Type == type && x.Code == code && x.Id != row.Id, ct))
                errors.Add("code", "category_save.err_duplicate_code");
        }
        errors.Required("name", name);
        errors.MaxLength("name", name, 100);
        errors.MaxLength("nameEn", nameEn, 100);
        errors.MaxLength("description", description, 500);
        if (color.Length > 0 && !HexColor().IsMatch(color) && !NamedColor().IsMatch(color)) errors.Add("color", "validation.invalid_value");
        var sortSeq = errors.Number("sortSeq", r.SortSeq, 1, 999, integer: true);
        // Performance levels carry the coefficient used by Actual1 of the effort grid (D12).
        if (type == MasterDataTypes.PerformanceLevel)
        {
            JsonElement? coefficient = metadata["coefficient"] is { } c ? JsonSerializer.SerializeToElement(c) : null;
            errors.Number("coefficient", coefficient, 0, 2);
        }
        if (metadata.Any(p => !MetadataKey().IsMatch(p.Key))) errors.Add("metadata", "category_save.err_metadata_key");
        errors.ThrowIfAny();

        if (color.Length > 0) metadata["color"] = color;
        else metadata.Remove("color");

        row.Type = type;
        row.Code = code;
        row.Name = name;
        row.NameEn = Text.OrNull(nameEn);
        row.Description = Text.OrNull(description);
        row.SortOrder = (int)sortSeq!.Value;
        row.Metadata = metadata.ToJsonString();
        row.IsActive = r.IsActive != false;
    }

    private static CategoryDto ToDto(MasterDatum m) => new(
        m.Id, m.Type, m.Code, m.Name, m.NameEn, m.Description, Localized.Color(m.Metadata), m.SortOrder,
        Localized.Json(m.Metadata) ?? JsonSerializer.SerializeToElement(new { }), m.IsActive, m.IsSystem, m.UpdatedAt);

    [GeneratedRegex("^[A-Z0-9_]{1,30}$")]
    private static partial Regex CodePattern();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();

    [GeneratedRegex("^[a-z]{2,10}$")]
    private static partial Regex NamedColor();

    [GeneratedRegex("^[a-z][a-z0-9_]{0,39}$")]
    private static partial Regex MetadataKey();
}
