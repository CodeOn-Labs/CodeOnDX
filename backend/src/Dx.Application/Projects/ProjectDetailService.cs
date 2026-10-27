using System.Text.Json;
using System.Text.RegularExpressions;
using Dx.Application.Common;
using Dx.Application.MasterData;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Projects;

public sealed record ContractStaffDto(
    Guid Id, string RoleCode, string RoleName, MasterRefDto? Level, int Quantity, decimal FtePerPerson,
    DateOnly StartDate, DateOnly EndDate, decimal PricePerMonth);

/// <summary>Someone assigned to the project who left / is leaving and was not replaced yet.</summary>
public sealed record HrAlertDto(Guid EmployeeId, string FullName, string Code, string Kind, DateOnly? LeftDate);

/// <summary>GET /projects/{id} — list row + details + what the user may do.</summary>
public sealed record ProjectDetailDto(
    Guid Id, string Code, string Name, ClientRefDto? Client, MasterRefDto? Grade, MasterRefDto? Status, MasterRefDto? RiskLevel,
    DateOnly? StartDate, DateOnly? EndDate, decimal? Budget, string? CurrencyCode, int ProgressPercent,
    decimal ContractStaffFte, decimal ActualStaffFte, decimal BudgetUsedPercent, bool IsDeleted, DateTime CreatedAt,
    string Description, string? TicketSystemUrl, string? SourceCodeUrl, string? DriveUrl,
    IReadOnlyList<ContractStaffDto> ContractStaff, decimal BudgetWarningThreshold, IReadOnlyList<HrAlertDto> HrAlerts,
    bool CanManage, bool CanEditMonthlyReport, bool CanManagePcv, bool CanManageCss);

public sealed record ContractStaffRequest(
    string? Id, string? RoleCode, string? LevelId, JsonElement? Quantity, JsonElement? FtePerPerson, JsonElement? PricePerMonth,
    string? StartDate, string? EndDate);

public sealed record SaveProjectRequest(
    string? Name, string? ClientId, string? GradeId, string? StatusId, string? Description, string? StartDate, string? EndDate,
    JsonElement? Budget, string? TicketSystemUrl, string? SourceCodeUrl, IReadOnlyList<ContractStaffRequest>? ContractStaff);

public sealed record ImportProjectRow(int Row, Dictionary<string, JsonElement>? Values);

public sealed record ImportProjectsRequest(IReadOnlyList<ImportProjectRow>? Rows);

public sealed record ImportErrorDto(int Row, string? Field, string Message);

public sealed record ImportProjectsResult(int Created, IReadOnlyList<ImportErrorDto> Errors);

/// <summary>
/// Project detail, create / edit / trash / restore and Excel import. A project always has at
/// least one contract position (project_contract_staff); its links (ticket system, source code)
/// are project_links rows typed by master_data link_type.
/// </summary>
public sealed partial class ProjectDetailService(
    IAppDbContext db, ICurrentUser user, ProjectAccess access, ProjectService projects, RuleService rules, ITenantClock clock,
    IMessageLocalizer localizer)
{
    private static readonly string[] TicketLinkCodes = ["JIRA", "BACKLOG"];
    private static readonly string[] SourceLinkCodes = ["GITHUB", "GITLAB"];
    private const string DriveLinkCode = "GOOGLE_DRIVE";

    public async Task<ProjectDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var scope = await access.OpenAsync(id, ct);
        return await DetailAsync(scope, ct);
    }

    /// <summary>POST /projects — new projects start PLANNING, risk LOW, code PRJ-nn, currency of the client.</summary>
    public async Task<ProjectDetailDto> CreateAsync(SaveProjectRequest request, CancellationToken ct)
    {
        var input = await ParseAsync(request, isCreate: true, ct);
        var project = await NewProjectAsync(input, riskLevelId: null, ct);
        await db.SaveChangesAsync(ct);
        return await DetailAsync(new ProjectScope(project, true, null), ct);
    }

    public async Task<ProjectDetailDto> UpdateAsync(Guid id, SaveProjectRequest request, CancellationToken ct)
    {
        var scope = await access.ManageAsync(id, ct, tracking: true);
        var input = await ParseAsync(request, isCreate: false, ct);
        var p = scope.Project;
        await ApplyContractStaffAsync(p, input, ct);
        p.Name = input.Name;
        p.ClientId = input.ClientId;
        p.GradeId = input.GradeId;
        p.StatusId = input.StatusId!.Value;
        p.Description = input.Description;
        p.StartDate = input.StartDate;
        p.EndDate = input.EndDate;
        p.Budget = input.Budget ?? 0;
        await UpsertLinkAsync(p.Id, TicketLinkCodes, input.TicketSystemUrl, "Ticket", ct);
        await UpsertLinkAsync(p.Id, SourceLinkCodes, input.SourceCodeUrl, "Source code", ct);
        await db.SaveChangesAsync(ct);
        return await DetailAsync(scope with { CanManage = true }, ct);
    }

    /// <summary>DELETE /projects/{id} — to the trash. Returns the name for the message.</summary>
    public async Task<string> DeleteAsync(Guid id, CancellationToken ct)
    {
        var scope = await access.ManageAsync(id, ct, tracking: true);
        scope.Project.IsDeleted = true;
        scope.Project.DeletedAt = DateTime.UtcNow;
        scope.Project.DeletedBy = user.UserId;
        await db.SaveChangesAsync(ct);
        return scope.Project.Name;
    }

    /// <summary>POST /projects/{id}/restore — needs project.manage (checked by the controller).</summary>
    public async Task<string> RestoreAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Set<Project>().IgnoreQueryFilters([QueryFilters.SoftDelete]).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException();
        p.IsDeleted = false;
        p.DeletedAt = null;
        p.DeletedBy = null;
        await db.SaveChangesAsync(ct);
        return p.Name;
    }

    /// <summary>
    /// POST /projects/import — rows read from the Excel template by the browser. One row = one
    /// project + one contract position; rows with the same project name (case-insensitive) make
    /// one project with several positions. Client, grade, risk, position and level are matched by
    /// name, English name or code. A project with any error is skipped; the others are created.
    /// Defaults: grade B, risk LOW, level JUNIOR; no position at all → 1 PM SENIOR at 0.5 FTE.
    /// </summary>
    public async Task<ImportProjectsResult> ImportAsync(ImportProjectsRequest request, CancellationToken ct)
    {
        var rows = (request.Rows ?? []).Take(5000).ToList();
        var masters = await db.Set<MasterDatum>().AsNoTracking().Where(m => m.IsActive).ToListAsync(ct);
        var clients = await db.Set<Client>().AsNoTracking().ToListAsync(ct);
        string Norm(string? s) => Text.Clean(s).ToLowerInvariant();
        MasterDatum? ByNameOrCode(string type, string? value)
        {
            var v = Norm(value);
            return v.Length == 0 ? null : masters.FirstOrDefault(m => m.Type == type && (Norm(m.Name) == v || Norm(m.NameEn) == v || Norm(m.Code) == v));
        }
        MasterDatum? ByCode(string type, string code) => masters.FirstOrDefault(m => m.Type == type && m.Code == code);

        var created = 0;
        var errors = new List<(int Row, string? Field, string Key, Dictionary<string, object?>? Args)>();
        foreach (var group in rows.GroupBy(r => Norm(Value(r, "name"))))
        {
            var list = group.ToList();
            var first = list[0];
            var rowErrors = new List<(int Row, string? Field, string Key, Dictionary<string, object?>? Args)>();
            var clientText = Value(first, "client");
            var client = clients.FirstOrDefault(c => Norm(c.Name) == Norm(clientText) || Norm(c.Code) == Norm(clientText));
            if (client is null) rowErrors.Add((first.Row, null, "project_import.err_client_not_found", new() { ["value"] = clientText }));
            var gradeText = Value(first, "grade");
            var grade = gradeText.Length > 0 ? ByNameOrCode(MasterDataTypes.ProjectLevel, gradeText) : ByCode(MasterDataTypes.ProjectLevel, "B");
            if (gradeText.Length > 0 && grade is null) rowErrors.Add((first.Row, null, "project_import.err_grade_not_found", new() { ["value"] = gradeText }));
            var riskText = Value(first, "risk");
            var risk = riskText.Length > 0 ? ByNameOrCode(MasterDataTypes.RiskLevel, riskText) : ByCode(MasterDataTypes.RiskLevel, "LOW");
            if (riskText.Length > 0 && risk is null) rowErrors.Add((first.Row, null, "project_import.err_risk_not_found", new() { ["value"] = riskText }));

            var staffRows = list.Where(r => Value(r, "role").Length > 0).ToList();
            var staff = staffRows.Select(r =>
            {
                var role = ByNameOrCode(MasterDataTypes.JobPosition, Value(r, "role"));
                var level = ByNameOrCode(MasterDataTypes.HrLevel, Value(r, "roleLevel")) ?? ByCode(MasterDataTypes.HrLevel, "JUNIOR");
                if (role is null) rowErrors.Add((r.Row, null, "project_import.err_role_not_found", new() { ["value"] = Value(r, "role") }));
                return new ContractStaffRequest(null, role?.Code ?? string.Empty, level?.Id.ToString() ?? string.Empty,
                    Raw(r, "quantity", 1), Raw(r, "fte", 1), Raw(r, "price", 0), Value(r, "roleStart"), Value(r, "roleEnd"));
            }).ToList();
            if (staff.Count == 0)
            {
                staff.Add(new ContractStaffRequest(null, "PM", ByCode(MasterDataTypes.HrLevel, "SENIOR")?.Id.ToString(),
                    JsonSerializer.SerializeToElement(1), JsonSerializer.SerializeToElement(0.5m), JsonSerializer.SerializeToElement(0), null, null));
            }

            if (rowErrors.Count == 0)
            {
                try
                {
                    var input = await ParseAsync(new SaveProjectRequest(
                        Value(first, "name"), client!.Id.ToString(), grade?.Id.ToString(), null, Value(first, "description"),
                        Value(first, "startDate"), Value(first, "endDate"), Raw(first, "budget", null),
                        Value(first, "ticketSystem"), Value(first, "sourceCode"), staff), isCreate: true, ct);
                    await NewProjectAsync(input, risk?.Id, ct);
                    await db.SaveChangesAsync(ct);
                    created++;
                }
                catch (AppValidationException ex)
                {
                    foreach (var (field, fieldErrors) in ex.Errors)
                    {
                        var m = StaffField().Match(field);
                        var row = m.Success && int.Parse(m.Groups[1].Value) < staffRows.Count ? staffRows[int.Parse(m.Groups[1].Value)].Row : first.Row;
                        rowErrors.AddRange(fieldErrors.Select(e => (row, (string?)field, e.Key, e.Args)));
                    }
                }
            }
            errors.AddRange(rowErrors);
        }
        var culture = user.Culture;
        return new ImportProjectsResult(created,
            errors.Select(e => new ImportErrorDto(e.Row, e.Field, localizer.Text(e.Key, e.Args, culture))).ToList());
    }

    // ─── detail ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Detail of a project. A user without project.manage only sees their own share (as in the
    /// original product):
    ///   contractStaffFte = Σ fte_per_person of the contract positions the user holds
    ///   actualStaffFte   = hours the user logged on the project ÷ (Mon–Fri days of the project × TIMESHEET_FTE_HOURS)
    ///                      e.g. 160 h ÷ (100 days × 8 h) = 0.2
    /// progressPercent = (today − start) ÷ (end − start) × 100, clamped to 0–100 and rounded down;
    /// without both dates (or end ≤ start) the manually entered progress is used.
    /// </summary>
    private async Task<ProjectDetailDto> DetailAsync(ProjectScope scope, CancellationToken ct)
    {
        var p = await db.Set<Project>().IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking()
            .Include(x => x.Client).Include(x => x.Grade).Include(x => x.Status).Include(x => x.RiskLevel)
            .FirstAsync(x => x.Id == scope.Project.Id, ct);
        var item = (await projects.ToListItemsAsync([p], ct))[0];
        var today = await clock.TodayAsync(ct);
        var culture = user.Culture;

        var staff = await db.Set<ProjectContractStaff>().AsNoTracking().Include(s => s.Position).Include(s => s.Level)
            .Where(s => s.ProjectId == p.Id).OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToListAsync(ct);
        var contractFte = item.ContractStaffFte;
        var actualFte = item.ActualStaffFte;
        if (!scope.CanManage)
        {
            var me = scope.EmployeeId;
            var mySlots = await db.Set<ProjectAssignment>().AsNoTracking()
                .Where(a => a.ProjectId == p.Id && a.EmployeeId == me && a.ContractStaffId != null).Select(a => a.ContractStaffId!.Value).ToListAsync(ct);
            contractFte = ProjectDates.Round2(staff.Where(s => mySlots.Contains(s.Id)).Sum(s => s.FtePerPerson));
            var hours = await db.Set<TimesheetEntry>().AsNoTracking().Where(t => t.ProjectId == p.Id && t.EmployeeId == me).SumAsync(t => t.Hours, ct);
            var days = p.StartDate is { } s0 && p.EndDate is { } e0 ? ProjectDates.WorkingDaysBetween(s0, e0) : 0;
            var fteHours = await rules.GetNumberAsync(RuleKeys.TimesheetFteHours, 8, ct);
            actualFte = days > 0 ? ProjectDates.Round2(hours / (days * fteHours)) : 0;
        }

        var links = await db.Set<ProjectLink>().AsNoTracking().Where(l => l.ProjectId == p.Id)
            .OrderBy(l => l.SortOrder).Select(l => new { l.Url, l.LinkType.Code }).ToListAsync(ct);
        string? LinkOf(params string[] codes) => links.FirstOrDefault(l => codes.Contains(l.Code))?.Url;

        return new ProjectDetailDto(
            item.Id, item.Code, item.Name, item.Client, item.Grade, item.Status, item.RiskLevel, item.StartDate, item.EndDate,
            item.Budget, item.CurrencyCode, ProgressOf(p, today), contractFte, actualFte, item.BudgetUsedPercent, item.IsDeleted, item.CreatedAt,
            p.Description ?? string.Empty, LinkOf(TicketLinkCodes), LinkOf(SourceLinkCodes), LinkOf(DriveLinkCode),
            scope.CanManage ? staff.Select(s => new ContractStaffDto(
                s.Id, s.Position.Code, Localized.Name(culture, s.Position.Name, s.Position.NameEn), MasterDataService.Ref(s.Level, culture),
                s.Quantity, s.FtePerPerson, s.StartDate, s.EndDate, s.MonthlyRate ?? 0)).ToList() : [],
            await rules.GetNumberAsync(RuleKeys.ProjectBudgetWarningThreshold, 75, ct),
            await HrAlertsAsync(p.Id, scope.CanManage ? null : scope.EmployeeId ?? Guid.Empty, today, ct),
            scope.CanManage, true, user.Has(Permissions.PcvManage), user.Has(Permissions.CssManage));
    }

    public static int ProgressOf(Project p, DateOnly today)
    {
        if (p.StartDate is { } start && p.EndDate is { } end && end > start)
        {
            var pct = (decimal)(today.DayNumber - start.DayNumber) / (end.DayNumber - start.DayNumber) * 100;
            return (int)Math.Floor(Math.Clamp(pct, 0, 100));
        }
        return p.ProgressPercent;
    }

    /// <summary>
    /// People assigned to the project whose leave date falls on / before the end of their
    /// assignment (or who already resigned) and whose position was not taken over by someone
    /// else later: kind "resigned" when the date has passed, "leaving_soon" otherwise.
    /// </summary>
    private async Task<IReadOnlyList<HrAlertDto>> HrAlertsAsync(Guid projectId, Guid? onlyEmployeeId, DateOnly today, CancellationToken ct)
    {
        var assignments = await db.Set<ProjectAssignment>().AsNoTracking().Where(a => a.ProjectId == projectId)
            .Select(a => new
            {
                a.Id, a.EmployeeId, a.ContractStaffId, a.SlotIndex, a.StartDate, a.EndDate,
                LastEnd = a.ProjectAssignmentPeriods.Max(x => (DateOnly?)x.EndDate),
                a.Employee.FullName, a.Employee.Code, a.Employee.TerminationDate, a.Employee.IsDeleted, StatusCode = a.Employee.Status.Code,
            })
            .ToListAsync(ct);
        var alerts = new List<HrAlertDto>();
        foreach (var a in assignments.Where(a => onlyEmployeeId is null || a.EmployeeId == onlyEmployeeId))
        {
            var replaced = a.ContractStaffId is not null && assignments.Any(b => b.Id != a.Id && b.ContractStaffId == a.ContractStaffId
                && b.SlotIndex == a.SlotIndex && b.StartDate > a.StartDate && b.EmployeeId != a.EmployeeId);
            var (resigned, leavingSoon) = LeaveFlags(a.StatusCode != MasterDataCodes.EmployeeStatusResigned && !a.IsDeleted, a.TerminationDate, a.LastEnd ?? a.EndDate, today);
            if (!replaced && (resigned || leavingSoon))
                alerts.Add(new HrAlertDto(a.EmployeeId, a.FullName, a.Code, resigned ? "resigned" : "leaving_soon", a.TerminationDate));
        }
        return alerts;
    }

    /// <summary>
    /// Leave state against an assignment ending <paramref name="assignmentEnd"/>: only a leave date
    /// on / before that end matters (no leave date but no longer active also counts).
    ///   resigned    = (not active or left ≤ today) and it matters
    ///   leavingSoon = not resigned, left &gt; today and it matters
    /// </summary>
    public static (bool Resigned, bool LeavingSoon) LeaveFlags(bool isActive, DateOnly? left, DateOnly? assignmentEnd, DateOnly today)
    {
        var conflict = left is not { } l || assignmentEnd is not { } end || l <= end;
        var resigned = (!isActive || (left is { } l1 && l1 <= today)) && conflict;
        var leavingSoon = !resigned && left is { } l2 && l2 > today && conflict;
        return (resigned, leavingSoon);
    }

    // ─── create / update ─────────────────────────────────────────────────────

    private sealed record StaffInput(int Index, Guid? Id, Guid PositionId, Guid LevelId, int Quantity, decimal FtePerPerson, decimal PricePerMonth, DateOnly Start, DateOnly End);

    private sealed record ProjectInput(
        string Name, Guid ClientId, Guid GradeId, Guid? StatusId, string Description, DateOnly? StartDate, DateOnly? EndDate,
        decimal? Budget, string TicketSystemUrl, string SourceCodeUrl, IReadOnlyList<StaffInput> ContractStaff);

    private async Task<Project> NewProjectAsync(ProjectInput input, Guid? riskLevelId, CancellationToken ct)
    {
        var planning = await MasterIdByCodeAsync(MasterDataTypes.ProjectStatus, MasterDataCodes.ProjectStatusPlanning, ct)
                       ?? throw new BusinessException("error.conflict");
        var client = await db.Set<Client>().AsNoTracking().FirstAsync(c => c.Id == input.ClientId, ct);
        var p = new Project
        {
            Id = Guid.CreateVersion7(),
            Code = await NextCodeAsync(ct),
            Name = input.Name,
            ClientId = input.ClientId,
            GradeId = input.GradeId,
            StatusId = planning,
            RiskLevelId = riskLevelId ?? await MasterIdByCodeAsync(MasterDataTypes.RiskLevel, "LOW", ct),
            Description = input.Description,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            Budget = input.Budget ?? 0,
            CurrencyCode = client.DefaultCurrencyCode ?? "USD",
        };
        db.Set<Project>().Add(p);
        await ApplyContractStaffAsync(p, input, ct);
        await UpsertLinkAsync(p.Id, TicketLinkCodes, input.TicketSystemUrl, "Ticket", ct);
        await UpsertLinkAsync(p.Id, SourceLinkCodes, input.SourceCodeUrl, "Source code", ct);
        return p;
    }

    /// <summary>Next code PRJ-nn after the highest existing one (trash included).</summary>
    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var codes = await db.Set<Project>().IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(p => p.Code.StartsWith("PRJ-")).Select(p => p.Code).ToListAsync(ct);
        var max = codes.Select(c => CodeNumber().Match(c)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        // Projects added in this unit of work (import) are not in the database yet.
        var pending = db.Set<Project>().Local.Select(p => CodeNumber().Match(p.Code)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        return $"PRJ-{Math.Max(max, pending) + 1:D2}";
    }

    /// <summary>
    /// Replaces the contract positions. A position removed, or whose quantity drops, must not
    /// still have someone assigned to a cut slot (400 project_save.err_staff_in_use).
    /// </summary>
    private async Task ApplyContractStaffAsync(Project p, ProjectInput input, CancellationToken ct)
    {
        var current = await db.Set<ProjectContractStaff>().Where(s => s.ProjectId == p.Id).ToListAsync(ct);
        var busy = await db.Set<ProjectAssignment>().AsNoTracking().Where(a => a.ProjectId == p.Id && a.ContractStaffId != null)
            .Select(a => new { StaffId = a.ContractStaffId!.Value, a.SlotIndex }).ToListAsync(ct);
        var errors = new ValidationErrors();
        foreach (var cs in current)
        {
            var row = input.ContractStaff.FirstOrDefault(r => r.Id == cs.Id);
            var keep = row?.Quantity ?? 0;
            if (busy.Any(b => b.StaffId == cs.Id && b.SlotIndex >= keep))
                errors.Add(row is null ? "contractStaff" : $"contractStaff.{row.Index}.quantity", "project_save.err_staff_in_use");
        }
        errors.ThrowIfAny();

        foreach (var cs in current.Where(cs => input.ContractStaff.All(r => r.Id != cs.Id)))
        {
            cs.IsDeleted = true;
            cs.DeletedAt = DateTime.UtcNow;
        }
        foreach (var r in input.ContractStaff)
        {
            var cs = r.Id is { } id ? current.FirstOrDefault(x => x.Id == id) : null;
            if (cs is null)
            {
                cs = new ProjectContractStaff { ProjectId = p.Id };
                db.Set<ProjectContractStaff>().Add(cs);
            }
            cs.PositionId = r.PositionId;
            cs.LevelId = r.LevelId;
            cs.Quantity = (short)r.Quantity;
            cs.FtePerPerson = r.FtePerPerson;
            cs.MonthlyRate = r.PricePerMonth;
            cs.StartDate = r.Start;
            cs.EndDate = r.End;
        }
    }

    /// <summary>Ticket system / source code link: provider from the URL (backlog / gitlab, else jira / github); empty removes it.</summary>
    private async Task UpsertLinkAsync(Guid projectId, string[] codes, string url, string label, CancellationToken ct)
    {
        var existing = await db.Set<ProjectLink>().Include(l => l.LinkType)
            .FirstOrDefaultAsync(l => l.ProjectId == projectId && codes.Contains(l.LinkType.Code), ct);
        if (url.Length == 0)
        {
            if (existing is not null) db.Set<ProjectLink>().Remove(existing);
            return;
        }
        var code = codes == TicketLinkCodes
            ? (url.Contains("backlog", StringComparison.OrdinalIgnoreCase) ? "BACKLOG" : "JIRA")
            : (url.Contains("gitlab", StringComparison.OrdinalIgnoreCase) ? "GITLAB" : "GITHUB");
        var typeId = await MasterIdByCodeAsync(MasterDataTypes.LinkType, code, ct);
        if (typeId is null) return; // the tenant removed that link type
        if (existing is null) db.Set<ProjectLink>().Add(new ProjectLink { ProjectId = projectId, LinkTypeId = typeId.Value, Name = label, Url = url });
        else
        {
            existing.Url = url;
            existing.LinkTypeId = typeId.Value;
        }
    }

    private async Task<ProjectInput> ParseAsync(SaveProjectRequest r, bool isCreate, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var name = Text.Clean(r.Name);
        errors.Required("name", name);
        errors.MaxLength("name", name, 200);
        var clientId = await RequiredRefAsync(errors, "clientId", r.ClientId,
            id => db.Set<Client>().AnyAsync(c => c.Id == id, ct));
        var gradeId = await RequiredRefAsync(errors, "gradeId", r.GradeId,
            id => db.Set<MasterDatum>().AnyAsync(m => m.Id == id && m.Type == MasterDataTypes.ProjectLevel, ct));
        Guid? statusId = null;
        if (!isCreate)
            statusId = await RequiredRefAsync(errors, "statusId", r.StatusId,
                id => db.Set<MasterDatum>().AnyAsync(m => m.Id == id && m.Type == MasterDataTypes.ProjectStatus, ct));
        var description = Text.Clean(r.Description);
        errors.MaxLength("description", description, 4000);
        var start = errors.Date("startDate", r.StartDate, optional: true);
        var end = errors.Date("endDate", r.EndDate, optional: true);
        if (start is { } s && end is { } e && e < s) errors.Add("endDate", "project_save.err_end_before_start");
        decimal? budget = null;
        if (r.Budget is { } b && b.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) && !(b.ValueKind == JsonValueKind.String && b.GetString()!.Length == 0))
            budget = errors.Number("budget", b, 0, null);
        var ticket = Text.Clean(r.TicketSystemUrl);
        var source = Text.Clean(r.SourceCodeUrl);
        foreach (var (field, value) in new[] { ("ticketSystemUrl", ticket), ("sourceCodeUrl", source) })
        {
            if (value.Length > 0 && !UrlPattern().IsMatch(value)) errors.Add(field, "validation.invalid_url");
            errors.MaxLength(field, value, 500);
        }

        var rows = r.ContractStaff ?? [];
        if (rows.Count == 0) errors.Add("contractStaff", "project_save.err_contract_staff_required");
        var positions = await db.Set<MasterDatum>().AsNoTracking().Where(m => m.Type == MasterDataTypes.JobPosition).ToDictionaryAsync(m => m.Code, m => m.Id, ct);
        var levels = (await db.Set<MasterDatum>().AsNoTracking().Where(m => m.Type == MasterDataTypes.HrLevel).Select(m => m.Id).ToListAsync(ct)).ToHashSet();
        var staff = new List<StaffInput>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            string F(string k) => $"contractStaff.{i}.{k}";
            var roleCode = Text.Clean(row.RoleCode);
            Guid positionId = Guid.Empty;
            if (errors.Required(F("roleCode"), roleCode) && !positions.TryGetValue(roleCode, out positionId)) errors.Add(F("roleCode"), "validation.invalid_value");
            var levelText = Text.Clean(row.LevelId);
            Guid levelId = Guid.Empty;
            if (errors.Required(F("levelId"), levelText) && (!Guid.TryParse(levelText, out levelId) || !levels.Contains(levelId))) errors.Add(F("levelId"), "validation.invalid_value");
            var quantity = errors.Number(F("quantity"), row.Quantity, 1, 100, integer: true);
            var fte = errors.Number(F("ftePerPerson"), row.FtePerPerson, 0.01m, 2);
            var price = IsBlank(row.PricePerMonth) ? 0 : errors.Number(F("pricePerMonth"), row.PricePerMonth, 0, null);
            var rs = errors.Date(F("startDate"), row.StartDate, optional: true) ?? start;
            var re = errors.Date(F("endDate"), row.EndDate, optional: true) ?? end;
            if (rs is null) errors.Add(F("startDate"), "validation.required");
            if (re is null) errors.Add(F("endDate"), "validation.required");
            if (rs is { } a && re is { } z && z < a) errors.Add(F("endDate"), "project_save.err_end_before_start");
            if (rs is { } a2 && start is { } ps && a2 < ps) errors.Add(F("startDate"), "project_save.err_staff_before_project");
            if (re is { } z2 && end is { } pe && z2 > pe) errors.Add(F("endDate"), "project_save.err_staff_after_project");
            if (!errors.Any)
                staff.Add(new StaffInput(i, Guid.TryParse(row.Id, out var sid) ? sid : null, positionId, levelId, (int)quantity!.Value, fte!.Value, price ?? 0, rs!.Value, re!.Value));
        }
        errors.ThrowIfAny();
        return new ProjectInput(name, clientId!.Value, gradeId!.Value, statusId, description, start, end, budget, ticket, source, staff);
    }

    private static async Task<Guid?> RequiredRefAsync(ValidationErrors errors, string field, string? raw, Func<Guid, Task<bool>> exists)
    {
        var text = Text.Clean(raw);
        if (!errors.Required(field, text)) return null;
        if (Guid.TryParse(text, out var id) && await exists(id)) return id;
        errors.Add(field, "validation.invalid_value");
        return null;
    }

    private Task<Guid?> MasterIdByCodeAsync(string type, string code, CancellationToken ct)
        => db.Set<MasterDatum>().AsNoTracking().Where(m => m.Type == type && m.Code == code).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);

    private static bool IsBlank(JsonElement? e)
        => e is not { } v || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || (v.ValueKind == JsonValueKind.String && v.GetString()!.Trim().Length == 0);

    /// <summary>Import cell as text (numbers / dates typed in Excel arrive as JSON numbers or strings).</summary>
    private static string Value(ImportProjectRow r, string key)
        => r.Values is not null && r.Values.TryGetValue(key, out var v)
            ? v.ValueKind == JsonValueKind.String ? v.GetString()!.Trim() : v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? string.Empty : v.GetRawText()
            : string.Empty;

    /// <summary>Import cell kept as JSON for number checks; empty / 0 / missing → <paramref name="fallback"/> (JS "x || fallback").</summary>
    private static JsonElement? Raw(ImportProjectRow r, string key, decimal? fallback)
    {
        if (r.Values is not null && r.Values.TryGetValue(key, out var v) && !IsBlank(v)
            && !(v.ValueKind == JsonValueKind.Number && v.GetDecimal() == 0 && fallback is not null))
            return v;
        return fallback is { } f ? JsonSerializer.SerializeToElement(f) : null;
    }

    [GeneratedRegex(@"^https?://\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"^PRJ-(\d+)$")]
    private static partial Regex CodeNumber();

    [GeneratedRegex(@"^contractStaff\.(\d+)\.")]
    private static partial Regex StaffField();
}
