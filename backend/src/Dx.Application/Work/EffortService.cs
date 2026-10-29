using System.Text.Json;
using Dx.Application.Common;
using Dx.Application.MasterData;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Work;

public sealed record EffortRowDto(
    Guid Id, string Month, WorkEmployeeRefDto? Employee, Guid? TeamId, string? TeamName, string? HomeTeamName,
    Guid? DepartmentId, string? DepartmentName, Guid ProjectId, string ProjectName, string Role, string? Note,
    decimal Hd, decimal TtEffort, decimal OtEffort, MasterRefDto? PerformanceLevel, decimal Performance, int AllocateRate);

public sealed record EffortUnitDto(Guid Id, string Name);

public sealed record EffortTeamDto(Guid Id, string Name, Guid DepartmentId);

public sealed record EffortProjectDto(Guid Id, string Name, Guid? DepartmentId);

public sealed record PerformanceLevelDto(Guid Id, string Code, string Name, string? Color, decimal Coefficient);

public sealed record EffortGridDto(
    string Month, bool CanEdit, IReadOnlyList<EffortRowDto> Rows, IReadOnlyList<EffortUnitDto> Departments, IReadOnlyList<EffortTeamDto> Teams,
    IReadOnlyList<EffortProjectDto> Projects, IReadOnlyList<string> Roles, IReadOnlyList<PerformanceLevelDto> PerformanceLevels);

public sealed record AddEffortRequest(string? Month, string? EmployeeId, string? DepartmentId, string? ProjectId, string? TeamId, string? Role);

/// <summary>
/// Effort grid (Công số): one row = one employee × project × position × month. A project belongs
/// to one department (project_cost_centers), which is the row's department. "Member" fields
/// (performance level / coefficient) are the same on every row of a person in a month; planned
/// (hd) and actual effort (ttEffort) belong to the row, and saving the plan alone sets the actual
/// effort to the same value. OT effort and the performance level are read-only on the grid (synced
/// from other systems) but still accepted here for that sync. Actual / Actual1 / Free / Free1 are
/// computed by the FE (effort-calculations.ts, D12).
/// Rows are addressed by the position NAME (column "role" of the grid).
/// </summary>
public sealed class EffortService(IAppDbContext db, ICurrentUser user, ITenantClock clock)
{
    /// <summary>GET /effort?month=yyyy-MM — rows by employee name then project name, plus the pickers.</summary>
    public async Task<EffortGridDto> GridAsync(string? monthParam, CancellationToken ct)
    {
        var month = await MonthAsync(monthParam, ct);
        var rows = await RowsAsync(db.Set<EffortAllocation>().Where(r => r.PeriodMonth == month), ct);
        var vi = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: false);
        var culture = user.Culture;
        var centers = await db.Set<ProjectCostCenter>().AsNoTracking().ToDictionaryAsync(c => c.ProjectId, c => c.DepartmentId, ct);
        var masters = await db.Set<MasterDatum>().AsNoTracking()
            .Where(m => (m.Type == MasterDataTypes.JobPosition || m.Type == MasterDataTypes.PerformanceLevel) && m.IsActive)
            .OrderBy(m => m.SortOrder).ToListAsync(ct);
        return new EffortGridDto(
            month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), user.Has(Permissions.EffortEdit),
            rows.OrderBy(r => r.Employee?.FullName ?? string.Empty, vi).ThenBy(r => r.ProjectName, vi).ToList(),
            await db.Set<Department>().AsNoTracking().OrderBy(d => d.SortOrder).Select(d => new EffortUnitDto(d.Id, d.Name)).ToListAsync(ct),
            await db.Set<Team>().AsNoTracking().OrderBy(t => t.Department.SortOrder).ThenBy(t => t.SortOrder).ThenBy(t => t.Name)
                .Select(t => new EffortTeamDto(t.Id, t.Name, t.DepartmentId)).ToListAsync(ct),
            (await db.Set<Project>().AsNoTracking().OrderByDescending(p => p.CreatedAt).Select(p => new { p.Id, p.Name }).ToListAsync(ct))
                .Select(p => new EffortProjectDto(p.Id, p.Name, centers.TryGetValue(p.Id, out var d) ? d : null)).ToList(),
            masters.Where(m => m.Type == MasterDataTypes.JobPosition).Select(m => m.Name).ToList(),
            masters.Where(m => m.Type == MasterDataTypes.PerformanceLevel).Select(m => new PerformanceLevelDto(
                m.Id, m.Code, Localized.Name(culture, m.Name, m.NameEn), Localized.Color(m.Metadata), Coefficient(m.Metadata) ?? 0)).ToList());
    }

    /// <summary>
    /// PATCH /effort/{id} — fields saved when the user leaves a cell: hd / ttEffort / otEffort (0–2;
    /// hd without ttEffort also sets ttEffort = hd), allocateRate (0–100), performanceLevelId (copies
    /// its coefficient), teamId, role, note, projectId (+ departmentId: a project already mapped to
    /// another department is refused). Member fields are copied to the person's other rows of the
    /// month; answers all those rows.
    /// </summary>
    public async Task<(IReadOnlyList<EffortRowDto> Rows, string Name)> UpdateAsync(Guid id, IReadOnlyDictionary<string, JsonElement> body, CancellationToken ct)
    {
        var row = await db.Set<EffortAllocation>().Include(r => r.Employee).FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException();
        var errors = new ValidationErrors();
        var memberFields = false;
        Guid? newProject = null;
        Guid? mapDepartment = null;
        foreach (var (field, value) in body)
        {
            switch (field)
            {
                case "hd":
                    if (errors.Number(field, value, 0, 2) is { } hd)
                    {
                        row.ContractEffort = hd;
                        // Entering the plan fills the actual effort; PMO edits ttEffort on its own when the month changes.
                        if (!body.ContainsKey("ttEffort")) row.ActualEffort = hd;
                    }
                    break;
                case "ttEffort":
                    if (errors.Number(field, value, 0, 2) is { } tt) row.ActualEffort = tt;
                    break;
                case "otEffort":
                    if (errors.Number(field, value, 0, 2) is { } ot) row.OvertimeEffort = ot;
                    break;
                case "allocateRate":
                    if (errors.Number(field, value, 0, 100, integer: true) is { } rate) row.AllocationRate = (short)rate;
                    break;
                case "performanceLevelId":
                {
                    var level = Guid.TryParse(Str(value), out var lid)
                        ? await db.Set<MasterDatum>().AsNoTracking().FirstOrDefaultAsync(m => m.Id == lid && m.Type == MasterDataTypes.PerformanceLevel, ct)
                        : null;
                    if (level is null) errors.Add(field, "validation.invalid_value");
                    else { row.PerformanceLevelId = level.Id; row.PerformanceCoefficient = Coefficient(level.Metadata) ?? 1; memberFields = true; }
                    break;
                }
                case "teamId":
                    if (Guid.TryParse(Str(value), out var teamId) && await db.Set<Team>().AnyAsync(t => t.Id == teamId, ct)) row.TeamId = teamId;
                    else errors.Add(field, "validation.invalid_value");
                    break;
                case "role":
                {
                    var name = Str(value);
                    if (name.Length == 0) { errors.Add(field, "validation.required"); break; }
                    var position = await PositionByNameAsync(name, ct);
                    if (position is null) errors.Add(field, "validation.invalid_value");
                    else row.PositionId = position.Value;
                    break;
                }
                case "note":
                {
                    var note = Str(value);
                    errors.MaxLength(field, note, 100);
                    row.Note = note.Length == 0 ? null : note;
                    break;
                }
                case "projectId":
                {
                    var departmentId = Guid.TryParse(body.TryGetValue("departmentId", out var dep) ? Str(dep) : null, out var did) ? did
                        : await db.Set<ProjectCostCenter>().Where(c => c.ProjectId == row.ProjectId).Select(c => (Guid?)c.DepartmentId).FirstOrDefaultAsync(ct);
                    if (!Guid.TryParse(Str(value), out var projectId) || !await db.Set<Project>().AnyAsync(p => p.Id == projectId, ct)) { errors.Add(field, "validation.invalid_value"); break; }
                    var center = await db.Set<ProjectCostCenter>().AsNoTracking().FirstOrDefaultAsync(c => c.ProjectId == projectId, ct);
                    // A project belongs to one department only (PROJECT_ALREADY_MAPPED in the original).
                    if (center is not null && center.DepartmentId != departmentId) { errors.Add(field, "effort_update.err_project_mapped"); break; }
                    if (center is null) mapDepartment = departmentId;
                    newProject = projectId;
                    break;
                }
                case "departmentId":
                    break;
                default:
                    errors.Add(field, "validation.invalid_value");
                    break;
            }
        }
        var oldProject = row.ProjectId;
        if (newProject is { } np) row.ProjectId = np;
        if ((newProject is not null || body.ContainsKey("role"))
            && await db.Set<EffortAllocation>().AnyAsync(r => r.Id != row.Id && r.EmployeeId == row.EmployeeId && r.PeriodMonth == row.PeriodMonth
                                                            && r.ProjectId == row.ProjectId && r.PositionId == row.PositionId, ct))
            errors.Add("role", "effort_add.err_duplicate");
        errors.ThrowIfAny();

        if (newProject is { } p && mapDepartment is { } md) db.Set<ProjectCostCenter>().Add(new ProjectCostCenter { ProjectId = p, DepartmentId = md });
        var siblings = await db.Set<EffortAllocation>().Where(r => r.EmployeeId == row.EmployeeId && r.PeriodMonth == row.PeriodMonth && r.Id != row.Id).ToListAsync(ct);
        if (memberFields)
            foreach (var s in siblings)
                (s.PerformanceLevelId, s.PerformanceCoefficient) = (row.PerformanceLevelId, row.PerformanceCoefficient);
        await db.SaveChangesAsync(ct);

        // The old project has no row left in any month → its department mapping goes.
        if (newProject is not null && newProject != oldProject && !await db.Set<EffortAllocation>().AnyAsync(r => r.ProjectId == oldProject, ct))
        {
            var oldCenter = await db.Set<ProjectCostCenter>().FirstOrDefaultAsync(c => c.ProjectId == oldProject, ct);
            if (oldCenter is not null)
            {
                db.Set<ProjectCostCenter>().Remove(oldCenter);
                await db.SaveChangesAsync(ct);
            }
        }
        var ids = siblings.Select(s => s.Id).Append(row.Id).ToList();
        return (await RowsAsync(db.Set<EffortAllocation>().Where(r => ids.Contains(r.Id)), ct), row.Employee.FullName);
    }

    /// <summary>
    /// POST /effort — new row with plan / actual effort 0. A person who already has rows that month
    /// inherits their performance level; otherwise performance level CT (official contract), or the
    /// first level of the tenant's catalog when it has no CT.
    /// </summary>
    public async Task<(EffortRowDto Row, string Name)> AddAsync(AddEffortRequest r, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        DateOnly? month = WorkDates.Parse(Text.Clean(r.Month) + "-01");
        if (month is null) errors.Add("month", "validation.invalid_value");
        var employee = Guid.TryParse(r.EmployeeId, out var eid)
            ? await db.Set<Employee>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == eid && e.Status.Code != MasterDataCodes.EmployeeStatusResigned, ct)
            : null;
        if (employee is null) errors.Add("employeeId", "validation.required");
        var departmentOk = Guid.TryParse(r.DepartmentId, out var departmentId) && await db.Set<Department>().AnyAsync(d => d.Id == departmentId, ct);
        if (!departmentOk) errors.Add("departmentId", "validation.required");
        var projectOk = Guid.TryParse(r.ProjectId, out var projectId) && await db.Set<Project>().AnyAsync(p => p.Id == projectId, ct);
        if (!projectOk) errors.Add("projectId", "validation.required");
        var center = projectOk ? await db.Set<ProjectCostCenter>().AsNoTracking().FirstOrDefaultAsync(c => c.ProjectId == projectId, ct) : null;
        if (center is not null && center.DepartmentId != departmentId) errors.Add("projectId", "effort_update.err_project_mapped");
        if (!Guid.TryParse(r.TeamId, out var teamId) || !await db.Set<Team>().AnyAsync(t => t.Id == teamId, ct)) errors.Add("teamId", "validation.required");
        var role = Text.Clean(r.Role);
        Guid? positionId = null;
        if (role.Length == 0) errors.Add("role", "validation.required");
        else if ((positionId = await PositionByNameAsync(role, ct)) is null) errors.Add("role", "validation.invalid_value");
        if (month is { } m && employee is not null && positionId is { } pos
            && await db.Set<EffortAllocation>().AnyAsync(x => x.EmployeeId == employee.Id && x.PeriodMonth == m && x.ProjectId == projectId && x.PositionId == pos, ct))
            errors.Add("role", "effort_add.err_duplicate");
        errors.ThrowIfAny();

        if (center is null) db.Set<ProjectCostCenter>().Add(new ProjectCostCenter { ProjectId = projectId, DepartmentId = departmentId });
        var sibling = await db.Set<EffortAllocation>().AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employee!.Id && x.PeriodMonth == month, ct);
        var full = await db.Set<MasterDatum>().AsNoTracking().Where(x => x.Type == MasterDataTypes.PerformanceLevel)
            .OrderBy(x => x.Code == "CT" ? 0 : 1).ThenBy(x => x.SortOrder).FirstOrDefaultAsync(ct);
        var row = new EffortAllocation
        {
            EmployeeId = employee!.Id, ProjectId = projectId, PeriodMonth = month!.Value, TeamId = teamId, PositionId = positionId!.Value,
            ContractEffort = 0, ActualEffort = 0, OvertimeEffort = 0,
            PerformanceLevelId = sibling?.PerformanceLevelId ?? full?.Id,
            PerformanceCoefficient = sibling?.PerformanceCoefficient ?? Coefficient(full?.Metadata) ?? 1, AllocationRate = 100,
        };
        db.Set<EffortAllocation>().Add(row);
        await db.SaveChangesAsync(ct);
        // actual_effort has DB default 1 (V008) and EF leaves the CLR default 0 out of the INSERT,
        // so the row comes back with 1 → reset it: a new row starts with plan = actual = 0.
        if (row.ActualEffort != 0)
        {
            row.ActualEffort = 0;
            await db.SaveChangesAsync(ct);
        }
        return ((await RowsAsync(db.Set<EffortAllocation>().Where(x => x.Id == row.Id), ct)).Single(), employee.FullName);
    }

    public async Task<string> DeleteAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<EffortAllocation>().Include(r => r.Employee).FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException();
        db.Set<EffortAllocation>().Remove(row);
        await db.SaveChangesAsync(ct);
        return row.Employee.FullName;
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private async Task<DateOnly> MonthAsync(string? monthParam, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(monthParam))
        {
            var today = await clock.TodayAsync(ct);
            return new DateOnly(today.Year, today.Month, 1);
        }
        return WorkDates.Parse(monthParam + "-01") ?? throw AppValidationException.Single("month", "validation.invalid_value");
    }

    /// <summary>Grid column "role" holds the position name (Vietnamese name, or the code as typed in older data).</summary>
    private async Task<Guid?> PositionByNameAsync(string name, CancellationToken ct)
        => await db.Set<MasterDatum>().AsNoTracking()
            .Where(m => m.Type == MasterDataTypes.JobPosition && (m.Name == name || m.Code == name))
            .OrderBy(m => m.Name == name ? 0 : 1).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);

    private async Task<List<EffortRowDto>> RowsAsync(IQueryable<EffortAllocation> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking()
            .Include(r => r.Employee).ThenInclude(e => e.Team).Include(r => r.Project).Include(r => r.Team).Include(r => r.Position).Include(r => r.PerformanceLevel)
            .ToListAsync(ct);
        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var centers = await db.Set<ProjectCostCenter>().AsNoTracking().Include(c => c.Department)
            .Where(c => projectIds.Contains(c.ProjectId)).ToDictionaryAsync(c => c.ProjectId, ct);
        var culture = user.Culture;
        return rows.Select(r =>
        {
            var center = centers.GetValueOrDefault(r.ProjectId);
            return new EffortRowDto(
                r.Id, r.PeriodMonth.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), WorkScope.Ref(r.Employee),
                r.TeamId, r.Team?.Name, r.Employee.Team?.Name, center?.DepartmentId, center?.Department.Name,
                r.ProjectId, r.Project.Name, r.Position.Name, r.Note, r.ContractEffort, r.ActualEffort, r.OvertimeEffort,
                MasterDataService.Ref(r.PerformanceLevel, culture), r.PerformanceCoefficient, r.AllocationRate);
        }).ToList();
    }

    private static decimal? Coefficient(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        using var doc = JsonDocument.Parse(metadataJson);
        return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("coefficient", out var c) && c.TryGetDecimal(out var v) ? v : null;
    }

    private static string Str(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : string.Empty;
}
