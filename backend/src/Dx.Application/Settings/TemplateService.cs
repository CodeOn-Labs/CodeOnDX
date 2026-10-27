using Dx.Application.Common;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Settings;

public sealed record HolidayDto(Guid Id, DateOnly Date, string Name, bool IsPaid);

public sealed record CreateHolidayRequest(string? Date, string? Name, bool? IsPaid);

public sealed record PcvTemplateItemDto(Guid Id, string Category, string Label, int SortSeq);

public sealed record SavePcvTemplateItemRequest(string? Category, string? Label);

public sealed record CssTemplateDto(string Title, string Intro, string ScaleNote, string CommentLabel);

public sealed record SaveCssTemplateRequest(string? Title, string? Intro, string? ScaleNote, string? CommentLabel);

public sealed record CssTemplateQuestionDto(Guid Id, string Label, bool Required, int SortSeq);

public sealed record SaveCssTemplateQuestionRequest(string? Label, bool? Required);

/// <summary>Holidays (not counted as leave days) of the tenant.</summary>
public sealed class HolidayService(IAppDbContext db)
{
    /// <summary>GET /holidays?year=2026 — by date.</summary>
    public async Task<IReadOnlyList<HolidayDto>> ListAsync(int? year, CancellationToken ct)
    {
        var query = db.Set<Holiday>().AsNoTracking();
        if (year is { } y) query = query.Where(h => h.HolidayDate.Year == y);
        return await query.OrderBy(h => h.HolidayDate).Select(h => new HolidayDto(h.Id, h.HolidayDate, h.Name, h.IsPaid)).ToListAsync(ct);
    }

    public async Task<HolidayDto> CreateAsync(CreateHolidayRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var date = errors.Date("date", request.Date);
        var name = Text.Clean(request.Name);
        if (date is { } d && await db.Set<Holiday>().AnyAsync(h => h.HolidayDate == d, ct)) errors.Add("date", "holiday_create.err_duplicate_date");
        errors.Required("name", name);
        errors.MaxLength("name", name, 100);
        errors.ThrowIfAny();

        var row = new Holiday { HolidayDate = date!.Value, Name = name, IsPaid = request.IsPaid != false };
        db.Set<Holiday>().Add(row);
        await db.SaveChangesAsync(ct);
        return new HolidayDto(row.Id, row.HolidayDate, row.Name, row.IsPaid);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<Holiday>().FirstOrDefaultAsync(h => h.Id == id, ct) ?? throw new NotFoundException();
        db.Set<Holiday>().Remove(row);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Admin › PCV criteria and Admin › CSS form. Projects copy the template when they are created
/// (project_pcv_items, project_css_forms / questions), so editing it never changes existing projects.
/// </summary>
public sealed class TemplateService(IAppDbContext db)
{
    // ─── PCV criteria ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PcvTemplateItemDto>> ListPcvAsync(CancellationToken ct)
        => await db.Set<PcvTemplateItem>().AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt)
            .Select(p => new PcvTemplateItemDto(p.Id, p.Category, p.Content, p.SortOrder)).ToListAsync(ct);

    public async Task<PcvTemplateItemDto> CreatePcvAsync(SavePcvTemplateItemRequest request, CancellationToken ct)
    {
        var (category, label) = ValidatePcv(request);
        var next = (await db.Set<PcvTemplateItem>().MaxAsync(p => (int?)p.SortOrder, ct) ?? 0) + 1;
        var row = new PcvTemplateItem { Category = category, Content = label, SortOrder = next };
        db.Set<PcvTemplateItem>().Add(row);
        await db.SaveChangesAsync(ct);
        return new PcvTemplateItemDto(row.Id, row.Category, row.Content, row.SortOrder);
    }

    public async Task<PcvTemplateItemDto> UpdatePcvAsync(Guid id, SavePcvTemplateItemRequest request, CancellationToken ct)
    {
        var row = await db.Set<PcvTemplateItem>().FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException();
        (row.Category, row.Content) = ValidatePcv(request);
        await db.SaveChangesAsync(ct);
        return new PcvTemplateItemDto(row.Id, row.Category, row.Content, row.SortOrder);
    }

    /// <summary>Soft delete: projects that already copied the item keep their link (project_pcv_items.template_item_id).</summary>
    public async Task DeletePcvAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<PcvTemplateItem>().FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException();
        row.IsDeleted = true;
        row.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static (string Category, string Label) ValidatePcv(SavePcvTemplateItemRequest r)
    {
        var errors = new ValidationErrors();
        var category = Text.Clean(r.Category);
        var label = Text.Clean(r.Label);
        errors.Required("category", category);
        errors.MaxLength("category", category, 100);
        errors.Required("label", label);
        errors.MaxLength("label", label, 500);
        errors.ThrowIfAny();
        return (category, label);
    }

    // ─── CSS form ────────────────────────────────────────────────────────────

    public async Task<CssTemplateDto> GetCssAsync(CancellationToken ct)
        => ToDto(await db.Set<CssTemplate>().AsNoTracking().FirstOrDefaultAsync(ct));

    public async Task<CssTemplateDto> UpdateCssAsync(SaveCssTemplateRequest r, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var title = Text.Clean(r.Title);
        errors.Required("title", title);
        errors.MaxLength("title", title, 200);
        errors.MaxLength("intro", Text.Clean(r.Intro), 2000);
        errors.MaxLength("scaleNote", Text.Clean(r.ScaleNote), 2000);
        errors.MaxLength("commentLabel", Text.Clean(r.CommentLabel), 2000);
        errors.ThrowIfAny();

        var row = await TemplateRowAsync(ct);
        row.Title = title;
        row.Intro = Text.Clean(r.Intro);
        row.ScaleNote = Text.Clean(r.ScaleNote);
        row.CommentLabel = Text.Clean(r.CommentLabel);
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<IReadOnlyList<CssTemplateQuestionDto>> ListCssQuestionsAsync(CancellationToken ct)
        => await db.Set<CssTemplateQuestion>().AsNoTracking().OrderBy(q => q.SortOrder).ThenBy(q => q.CreatedAt)
            .Select(q => new CssTemplateQuestionDto(q.Id, q.Content, q.IsRequired, q.SortOrder)).ToListAsync(ct);

    public async Task<CssTemplateQuestionDto> CreateCssQuestionAsync(SaveCssTemplateQuestionRequest request, CancellationToken ct)
    {
        var (label, required) = ValidateQuestion(request);
        var template = await TemplateRowAsync(ct);
        var next = (await db.Set<CssTemplateQuestion>().MaxAsync(q => (int?)q.SortOrder, ct) ?? 0) + 1;
        var row = new CssTemplateQuestion { Template = template, Content = label, IsRequired = required, SortOrder = next };
        db.Set<CssTemplateQuestion>().Add(row);
        await db.SaveChangesAsync(ct);
        return new CssTemplateQuestionDto(row.Id, row.Content, row.IsRequired, row.SortOrder);
    }

    public async Task<CssTemplateQuestionDto> UpdateCssQuestionAsync(Guid id, SaveCssTemplateQuestionRequest request, CancellationToken ct)
    {
        var row = await db.Set<CssTemplateQuestion>().FirstOrDefaultAsync(q => q.Id == id, ct) ?? throw new NotFoundException();
        (row.Content, row.IsRequired) = ValidateQuestion(request);
        await db.SaveChangesAsync(ct);
        return new CssTemplateQuestionDto(row.Id, row.Content, row.IsRequired, row.SortOrder);
    }

    public async Task DeleteCssQuestionAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<CssTemplateQuestion>().FirstOrDefaultAsync(q => q.Id == id, ct) ?? throw new NotFoundException();
        row.IsDeleted = true;
        row.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The single css_templates row of the tenant, created empty on first write.</summary>
    private async Task<CssTemplate> TemplateRowAsync(CancellationToken ct)
    {
        var row = await db.Set<CssTemplate>().FirstOrDefaultAsync(ct);
        if (row is not null) return row;
        row = new CssTemplate { Title = string.Empty, Intro = string.Empty, ScaleNote = string.Empty, CommentLabel = string.Empty };
        db.Set<CssTemplate>().Add(row);
        return row;
    }

    private static (string Label, bool Required) ValidateQuestion(SaveCssTemplateQuestionRequest r)
    {
        var errors = new ValidationErrors();
        var label = Text.Clean(r.Label);
        errors.Required("label", label);
        errors.MaxLength("label", label, 500);
        errors.ThrowIfAny();
        return (label, r.Required != false);
    }

    private static CssTemplateDto ToDto(CssTemplate? t)
        => t is null ? new CssTemplateDto(string.Empty, string.Empty, string.Empty, string.Empty) : new CssTemplateDto(t.Title, t.Intro, t.ScaleNote, t.CommentLabel);
}
