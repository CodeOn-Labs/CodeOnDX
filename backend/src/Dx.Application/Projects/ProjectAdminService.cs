using System.Text.Json;
using System.Text.RegularExpressions;
using Dx.Application.Common;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Projects;

public sealed record DocumentDto(
    Guid Id, Guid GroupId, string Name, string Url, DateTime CreatedAt, DateTime UpdatedAt, string? CreatedByName, string? UpdatedByName);

public sealed record DocumentGroupDto(
    Guid Id, string Name, string? Description, int SortSeq, DateTime CreatedAt, DateTime UpdatedAt, string? CreatedByName, string? UpdatedByName,
    IReadOnlyList<DocumentDto> Documents);

public sealed record DocumentsSummaryDto(int GroupCount, int DocumentCount);

public sealed record DocumentsDto(IReadOnlyList<DocumentGroupDto> Groups, DocumentsSummaryDto Summary, bool CanEdit);

public sealed record RiskDto(
    Guid Id, string Code, string Name, string? Description, string Category, int Probability, int Impact, int Score, string Level,
    string Strategy, string? ResponsePlan, Guid? OwnerId, string? OwnerName, DateOnly DueDate, string Status, DateOnly IdentifiedAt,
    bool IsActive, bool IsOverdue, DateTime CreatedAt, DateTime UpdatedAt, string? CreatedByName, string? UpdatedByName);

public sealed record RiskSummaryDto(int Total, int Active, int HighOrCritical, int Overdue, int Resolved);

public sealed record RiskOwnerDto(Guid Id, string FullName, string Code);

public sealed record RisksDto(
    IReadOnlyList<RiskDto> Items, IReadOnlyDictionary<string, int> Matrix, RiskSummaryDto Summary, IReadOnlyList<RiskOwnerDto> Owners, bool CanEdit);

public sealed record ChangeDto(
    Guid Id, string Code, string FileName, string? Url, string Source, string ImpactLevel, IReadOnlyList<string> ImpactAreas, string? ImpactNote,
    DateTime CreatedAt, DateTime UpdatedAt, string? CreatedByName, string? UpdatedByName);

public sealed record ChangesSummaryDto(int Total, int Customer, int Internal, int High);

public sealed record ChangesDto(IReadOnlyList<ChangeDto> Items, ChangesSummaryDto Summary, bool CanEdit);

public sealed record IssueDto(
    Guid Id, string Type, string Kind, string Name, string Url, DateTime CreatedAt, DateTime UpdatedAt, string? CreatedByName, string? UpdatedByName);

public sealed record IssuesDto(IReadOnlyList<IssueDto> Items, IReadOnlyDictionary<string, int> Summary, bool CanEdit);

/// <summary>
/// "Project admin" tab: document groups / documents (links), risk register, change requests and
/// issue-tracker links. Read: anyone who may open the project; write: project.manage on a project
/// not in the trash. Deletes are soft. Codes R-01 / CR-01 = highest existing (deleted included) + 1,
/// never reused.
/// </summary>
public sealed partial class ProjectAdminService(IAppDbContext db, ProjectAccess access, ITenantClock clock)
{
    public static readonly string[] Segments = ["document-groups", "documents", "risks", "changes", "issues"];

    private static readonly string[] RiskCategories = ["schedule", "technical", "resource", "customer", "cost", "quality"];
    private static readonly string[] RiskStrategies = ["avoid", "mitigate", "transfer", "accept"];
    private static readonly string[] RiskStatuses = ["open", "in_progress", "resolved", "closed"];
    private static readonly string[] ChangeSources = ["customer", "internal"];
    private static readonly string[] ChangeLevels = ["low", "medium", "high"];
    private static readonly string[] ChangeAreas = ["scope", "schedule", "cost", "quality"];
    private static readonly string[] IssueTypes = ["parent", "sprint", "milestone"];
    private static readonly string[] IssueKinds = ["task", "bug"];

    /// <summary>GET list of a segment (document-groups and documents both answer the documents view).</summary>
    public async Task<object> ListAsync(Guid projectId, string segment, CancellationToken ct)
    {
        var scope = await access.OpenAsync(projectId, ct);
        var canEdit = scope.CanManage && !scope.Project.IsDeleted;
        return segment switch
        {
            "document-groups" or "documents" => await DocumentsAsync(projectId, canEdit, ct),
            "risks" => await RisksAsync(projectId, canEdit, ct),
            "changes" => await ChangesAsync(projectId, canEdit, ct),
            "issues" => await IssuesAsync(projectId, canEdit, ct),
            _ => throw new NotFoundException(),
        };
    }

    /// <summary>POST — returns (message key prefix, label for the message).</summary>
    public async Task<(string Key, string Label)> CreateAsync(Guid projectId, string segment, JsonElement body, CancellationToken ct)
    {
        var p = (await access.ManageAsync(projectId, ct)).Project;
        string label;
        switch (segment)
        {
            case "document-groups":
            {
                var (name, description) = ParseGroup(body);
                var next = (await db.Set<ProjectDocumentGroup>().IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(g => g.ProjectId == p.Id).MaxAsync(g => (int?)g.SortOrder, ct) ?? 0) + 1;
                db.Set<ProjectDocumentGroup>().Add(new ProjectDocumentGroup { ProjectId = p.Id, Name = name, Description = description, SortOrder = next });
                label = name;
                break;
            }
            case "documents":
            {
                var (name, url, groupId) = await ParseDocumentAsync(body, p.Id, ct);
                db.Set<ProjectDocument>().Add(new ProjectDocument { ProjectId = p.Id, GroupId = groupId, Name = name, Url = url });
                label = name;
                break;
            }
            case "risks":
            {
                var row = new ProjectRisk { ProjectId = p.Id };
                await ApplyRiskAsync(row, body, ct);
                var codes = await db.Set<ProjectRisk>().IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(r => r.ProjectId == p.Id).Select(r => r.Code).ToListAsync(ct);
                row.Code = NextCode("R", codes);
                db.Set<ProjectRisk>().Add(row);
                label = row.Code;
                break;
            }
            case "changes":
            {
                var row = new ProjectChangeRequest { ProjectId = p.Id };
                ApplyChange(row, body);
                var codes = await db.Set<ProjectChangeRequest>().IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(r => r.ProjectId == p.Id).Select(r => r.Code).ToListAsync(ct);
                row.Code = NextCode("CR", codes);
                db.Set<ProjectChangeRequest>().Add(row);
                label = row.Code;
                break;
            }
            case "issues":
            {
                var row = new ProjectIssueLink { ProjectId = p.Id };
                ApplyIssue(row, body);
                db.Set<ProjectIssueLink>().Add(row);
                label = row.Name;
                break;
            }
            default:
                throw new NotFoundException();
        }
        await db.SaveChangesAsync(ct);
        return (KeyOf(segment), label);
    }

    public async Task<(string Key, string Label)> UpdateAsync(Guid projectId, string segment, Guid id, JsonElement body, CancellationToken ct)
    {
        var p = (await access.ManageAsync(projectId, ct)).Project;
        string label;
        switch (segment)
        {
            case "document-groups":
            {
                var row = await db.Set<ProjectDocumentGroup>().FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
                (row.Name, row.Description) = ParseGroup(body);
                label = row.Name;
                break;
            }
            case "documents":
            {
                var row = await db.Set<ProjectDocument>().FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
                (row.Name, row.Url, row.GroupId) = await ParseDocumentAsync(body, p.Id, ct);
                label = row.Name;
                break;
            }
            case "risks":
            {
                var row = await db.Set<ProjectRisk>().FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
                await ApplyRiskAsync(row, body, ct);
                label = row.Code;
                break;
            }
            case "changes":
            {
                var row = await db.Set<ProjectChangeRequest>().FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
                ApplyChange(row, body);
                label = row.Code;
                break;
            }
            case "issues":
            {
                var row = await db.Set<ProjectIssueLink>().FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
                ApplyIssue(row, body);
                label = row.Name;
                break;
            }
            default:
                throw new NotFoundException();
        }
        await db.SaveChangesAsync(ct);
        return (KeyOf(segment), label);
    }

    public async Task<(string Key, string Label)> DeleteAsync(Guid projectId, string segment, Guid id, CancellationToken ct)
    {
        var p = (await access.ManageAsync(projectId, ct)).Project;
        var now = DateTime.UtcNow;
        string label;
        switch (segment)
        {
            case "document-groups":
            {
                var row = await db.Set<ProjectDocumentGroup>().FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
                (row.IsDeleted, row.DeletedAt, label) = (true, now, row.Name);
                break;
            }
            case "documents":
            {
                var row = await db.Set<ProjectDocument>().FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
                (row.IsDeleted, row.DeletedAt, label) = (true, now, row.Name);
                break;
            }
            case "risks":
            {
                var row = await db.Set<ProjectRisk>().FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
                (row.IsDeleted, row.DeletedAt, label) = (true, now, row.Code);
                break;
            }
            case "changes":
            {
                var row = await db.Set<ProjectChangeRequest>().FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
                (row.IsDeleted, row.DeletedAt, label) = (true, now, row.Code);
                break;
            }
            case "issues":
            {
                var row = await db.Set<ProjectIssueLink>().FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
                (row.IsDeleted, row.DeletedAt, label) = (true, now, row.Name);
                break;
            }
            default:
                throw new NotFoundException();
        }
        await db.SaveChangesAsync(ct);
        return (KeyOf(segment), label);
    }

    // ─── lists ───────────────────────────────────────────────────────────────

    public async Task<DocumentsDto> DocumentsAsync(Guid projectId, bool canEdit, CancellationToken ct)
    {
        var groups = await db.Set<ProjectDocumentGroup>().AsNoTracking().Where(g => g.ProjectId == projectId).OrderBy(g => g.SortOrder).ToListAsync(ct);
        var docs = await db.Set<ProjectDocument>().AsNoTracking().Where(d => d.ProjectId == projectId).OrderByDescending(d => d.UpdatedAt).ToListAsync(ct);
        var names = await NamesAsync(groups.SelectMany(g => new[] { g.CreatedBy, g.UpdatedBy }).Concat(docs.SelectMany(d => new[] { d.CreatedBy, d.UpdatedBy })), ct);
        var dtos = groups.Select(g => new DocumentGroupDto(g.Id, g.Name, g.Description, g.SortOrder, g.CreatedAt, g.UpdatedAt, Name(names, g.CreatedBy), Name(names, g.UpdatedBy),
            docs.Where(d => d.GroupId == g.Id).Select(d => new DocumentDto(d.Id, g.Id, d.Name, d.Url, d.CreatedAt, d.UpdatedAt, Name(names, d.CreatedBy), Name(names, d.UpdatedBy))).ToList())).ToList();
        return new DocumentsDto(dtos, new DocumentsSummaryDto(dtos.Count, dtos.Sum(g => g.Documents.Count)), canEdit);
    }

    /// <summary>
    /// Risk register. score = probability (1–5) × impact (1–5) = 1–25; level: 1–4 low · 5–9 medium ·
    /// 10–16 high · 20–25 critical (not stored, changing the bands here changes them everywhere).
    /// Active = open / in_progress; overdue = active and due date before today. The 5×5 matrix
    /// (key "probability-impact") counts active risks only. Sorted by score, then code.
    /// </summary>
    public async Task<RisksDto> RisksAsync(Guid projectId, bool canEdit, CancellationToken ct)
    {
        var today = await clock.TodayAsync(ct);
        var rows = await db.Set<ProjectRisk>().AsNoTracking().Where(r => r.ProjectId == projectId).ToListAsync(ct);
        var ownerIds = rows.Where(r => r.OwnerEmployeeId != null).Select(r => r.OwnerEmployeeId!.Value).Distinct().ToList();
        var owners = await db.Set<Employee>().IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking().Where(e => ownerIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName, ct);
        var names = await NamesAsync(rows.SelectMany(r => new[] { r.CreatedBy, r.UpdatedBy }), ct);
        var items = rows.Select(r =>
        {
            var score = r.Probability * r.Impact;
            var active = IsActive(r.Status);
            return new RiskDto(r.Id, r.Code, r.Title, r.Description, r.Category, r.Probability, r.Impact, score, Level(score), r.Strategy, r.ResponsePlan,
                r.OwnerEmployeeId, r.OwnerEmployeeId is { } o ? owners.GetValueOrDefault(o) : null, r.DueDate, r.Status, r.IdentifiedDate,
                active, active && r.DueDate < today, r.CreatedAt, r.UpdatedAt, Name(names, r.CreatedBy), Name(names, r.UpdatedBy));
        }).OrderByDescending(r => r.Score).ThenBy(r => r.Code, StringComparer.Ordinal).ToList();
        var active = items.Where(r => r.IsActive).ToList();
        var matrix = active.GroupBy(r => $"{r.Probability}-{r.Impact}").ToDictionary(g => g.Key, g => g.Count());
        var team = await db.Set<ProjectAssignment>().AsNoTracking().Where(a => a.ProjectId == projectId)
            .OrderBy(a => a.CreatedAt).Select(a => new { a.Employee.Id, a.Employee.FullName, a.Employee.Code }).ToListAsync(ct);
        return new RisksDto(items, matrix,
            new RiskSummaryDto(items.Count, active.Count, active.Count(r => r.Score >= 10), items.Count(r => r.IsOverdue), items.Count(r => !r.IsActive)),
            team.DistinctBy(e => e.Id).Select(e => new RiskOwnerDto(e.Id, e.FullName, e.Code)).ToList(), canEdit);
    }

    public async Task<ChangesDto> ChangesAsync(Guid projectId, bool canEdit, CancellationToken ct)
    {
        var rows = await db.Set<ProjectChangeRequest>().AsNoTracking().Where(c => c.ProjectId == projectId).ToListAsync(ct);
        var names = await NamesAsync(rows.SelectMany(r => new[] { r.CreatedBy, r.UpdatedBy }), ct);
        var items = rows.OrderByDescending(c => CodeNumber(c.Code)).Select(c => new ChangeDto(c.Id, c.Code, c.Title, c.Url, c.Source, c.ImpactLevel, c.ImpactAreas, c.ImpactNote,
            c.CreatedAt, c.UpdatedAt, Name(names, c.CreatedBy), Name(names, c.UpdatedBy))).ToList();
        return new ChangesDto(items, new ChangesSummaryDto(items.Count, items.Count(c => c.Source == "customer"), items.Count(c => c.Source == "internal"),
            items.Count(c => c.ImpactLevel == "high")), canEdit);
    }

    public async Task<IssuesDto> IssuesAsync(Guid projectId, bool canEdit, CancellationToken ct)
    {
        var rows = await db.Set<ProjectIssueLink>().AsNoTracking().Where(i => i.ProjectId == projectId).ToListAsync(ct);
        var names = await NamesAsync(rows.SelectMany(r => new[] { r.CreatedBy, r.UpdatedBy }), ct);
        var vi = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: false);
        var items = rows.OrderBy(i => Array.IndexOf(IssueTypes, i.Category)).ThenBy(i => i.Name, vi)
            .Select(i => new IssueDto(i.Id, i.Category, i.IssueType, i.Name, i.Url, i.CreatedAt, i.UpdatedAt, Name(names, i.CreatedBy), Name(names, i.UpdatedBy))).ToList();
        return new IssuesDto(items, IssueTypes.ToDictionary(t => t, t => items.Count(i => i.Type == t)), canEdit);
    }

    // ─── parsing ─────────────────────────────────────────────────────────────

    private static (string Name, string? Description) ParseGroup(JsonElement body)
    {
        var errors = new ValidationErrors();
        var name = Str(body, "name");
        errors.Required("name", name);
        errors.MaxLength("name", name, 100);
        var description = Str(body, "description");
        errors.MaxLength("description", description, 500);
        errors.ThrowIfAny();
        return (name, Text.OrNull(description));
    }

    private async Task<(string Name, string Url, Guid GroupId)> ParseDocumentAsync(JsonElement body, Guid projectId, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var name = Str(body, "name");
        errors.Required("name", name);
        errors.MaxLength("name", name, 200);
        var url = Str(body, "url");
        UrlField(errors, "url", url);
        var groupText = Str(body, "groupId");
        var groupId = Guid.Empty;
        if (groupText.Length == 0) errors.Add("groupId", "validation.required");
        else if (!Guid.TryParse(groupText, out groupId) || !await db.Set<ProjectDocumentGroup>().AnyAsync(g => g.Id == groupId && g.ProjectId == projectId, ct))
            errors.Add("groupId", "validation.invalid_value");
        errors.ThrowIfAny();
        return (name, url, groupId);
    }

    private async Task ApplyRiskAsync(ProjectRisk row, JsonElement body, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var name = Str(body, "name");
        errors.Required("name", name);
        errors.MaxLength("name", name, 200);
        var description = Str(body, "description");
        errors.MaxLength("description", description, 2000);
        var category = OneOf(errors, body, "category", RiskCategories);
        var probability = errors.Number("probability", Prop(body, "probability"), 1, 5, integer: true);
        var impact = errors.Number("impact", Prop(body, "impact"), 1, 5, integer: true);
        var strategy = OneOf(errors, body, "strategy", RiskStrategies);
        var plan = Str(body, "responsePlan");
        errors.MaxLength("responsePlan", plan, 2000);
        var ownerId = errors.Id("ownerId", Str(body, "ownerId"));
        if (ownerId is { } o && !await db.Set<Employee>().AnyAsync(e => e.Id == o, ct)) errors.Add("ownerId", "validation.invalid_value");
        var due = errors.Date("dueDate", Str(body, "dueDate"));
        var status = OneOf(errors, body, "status", RiskStatuses);
        var identified = errors.Date("identifiedAt", Str(body, "identifiedAt"));
        if (identified is { } i && due is { } d && d < i) errors.Add("dueDate", "project_risk.err_due_before_identified");
        errors.ThrowIfAny();
        row.Title = name;
        row.Description = Text.OrNull(description);
        row.Category = category;
        row.Probability = (short)probability!.Value;
        row.Impact = (short)impact!.Value;
        row.Strategy = strategy;
        row.ResponsePlan = Text.OrNull(plan);
        row.OwnerEmployeeId = ownerId;
        row.DueDate = due!.Value;
        row.Status = status;
        row.IdentifiedDate = identified!.Value;
    }

    private static void ApplyChange(ProjectChangeRequest row, JsonElement body)
    {
        var errors = new ValidationErrors();
        var fileName = Str(body, "fileName");
        errors.Required("fileName", fileName);
        errors.MaxLength("fileName", fileName, 200);
        var url = Str(body, "url");
        UrlField(errors, "url", url);
        var source = OneOf(errors, body, "source", ChangeSources);
        var level = OneOf(errors, body, "impactLevel", ChangeLevels);
        var areas = Prop(body, "impactAreas") is { ValueKind: JsonValueKind.Array } a
            ? a.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : string.Empty).Distinct().ToList()
            : [];
        if (areas.Count == 0) errors.Add("impactAreas", "project_change.err_area_required");
        else if (areas.Any(x => !ChangeAreas.Contains(x))) errors.Add("impactAreas", "validation.invalid_value");
        var note = Str(body, "impactNote");
        errors.MaxLength("impactNote", note, 1000);
        errors.ThrowIfAny();
        row.Title = fileName;
        row.Url = url;
        row.Source = source;
        row.ImpactLevel = level;
        row.ImpactAreas = ChangeAreas.Where(areas.Contains).ToList();
        row.ImpactNote = Text.OrNull(note);
    }

    private static void ApplyIssue(ProjectIssueLink row, JsonElement body)
    {
        var errors = new ValidationErrors();
        var type = OneOf(errors, body, "type", IssueTypes);
        var kind = OneOf(errors, body, "kind", IssueKinds);
        var name = Str(body, "name");
        errors.Required("name", name);
        errors.MaxLength("name", name, 200);
        var url = Str(body, "url");
        UrlField(errors, "url", url);
        errors.ThrowIfAny();
        (row.Category, row.IssueType, row.Name, row.Url) = (type, kind, name, url);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    public static string Level(int score) => score >= 20 ? "critical" : score >= 10 ? "high" : score >= 5 ? "medium" : "low";

    public static bool IsActive(string status) => status is "open" or "in_progress";

    /// <summary>Next code PREFIX-nn after the highest one, e.g. R-07 → R-08.</summary>
    public static string NextCode(string prefix, IEnumerable<string> codes)
    {
        var max = codes.Select(c => Regex.Match(c.Trim(), $"^{Regex.Escape(prefix)}-(\\d+)$")).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        return $"{prefix}-{max + 1:D2}";
    }

    private static int CodeNumber(string code) => int.TryParse(CodeDigits().Match(code).Value, out var n) ? n : 0;

    private static string KeyOf(string segment) => segment switch
    {
        "document-groups" => "project_document_group",
        "documents" => "project_document",
        "risks" => "project_risk",
        "changes" => "project_change",
        _ => "project_issue",
    };

    private static void UrlField(ValidationErrors errors, string field, string value)
    {
        if (value.Length == 0) { errors.Add(field, "validation.required"); return; }
        if (!UrlPattern().IsMatch(value)) errors.Add(field, "validation.invalid_url");
        errors.MaxLength(field, value, 1000);
    }

    private static string OneOf(ValidationErrors errors, JsonElement body, string field, string[] allowed)
    {
        var value = Str(body, field);
        if (!allowed.Contains(value)) errors.Add(field, "validation.invalid_value");
        return value;
    }

    private static JsonElement? Prop(JsonElement body, string name)
        => body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out var v) ? v : null;

    private static string Str(JsonElement body, string name)
        => Prop(body, name) is { ValueKind: JsonValueKind.String } v ? v.GetString()!.Trim() : string.Empty;

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.OfType<Guid>().Distinct().ToList();
        return await db.Set<User>().AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static string? Name(Dictionary<Guid, string> names, Guid? id) => id is { } g ? names.GetValueOrDefault(g) : null;

    [GeneratedRegex(@"^https?://\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"\d+")]
    private static partial Regex CodeDigits();
}
