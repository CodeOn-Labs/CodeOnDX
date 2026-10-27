using System.Text.Json;
using Dx.Application.Common;
using Dx.Application.MasterData;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Work;

public sealed record TaskTypeRefDto(Guid Id, string Code, string Name);

public sealed record TimesheetEntryDto(
    Guid Id, DateOnly Date, Guid? ProjectId, string? ProjectName, TaskTypeRefDto? TaskType, decimal Hours, string? Description,
    string Status, string Source, string? RejectionReason, Guid EmployeeId);

public sealed record TimesheetRulesDto(decimal MinHours, decimal MaxHours, decimal? WeeklyMaxHours, decimal FteHours);

public sealed record TimesheetDayDto(DateOnly Date, bool Locked, string? LockReason, IReadOnlyDictionary<string, object?> LockArgs, bool Approved, bool Weekend, string? Holiday);

public sealed record DateRangeDto(DateOnly Start, DateOnly End);

public sealed record AssignedProjectDto(Guid Id, string Code, string Name, MasterRefDto? Status, IReadOnlyList<DateRangeDto> Ranges);

public sealed record TimesheetWeekDto(DateOnly Start, IReadOnlyDictionary<string, decimal> HoursByDate);

public sealed record TimesheetDto(
    Guid EmployeeId, DateOnly From, DateOnly To, bool CanEdit, TimesheetRulesDto Rules, IReadOnlyList<TimesheetEntryDto> Entries,
    IReadOnlyList<TimesheetDayDto> Days, IReadOnlyList<AssignedProjectDto> Projects, TimesheetWeekDto Week, IReadOnlyList<TaskTypeRefDto> TaskTypes);

public sealed record TimesheetProjectGroupDto(Guid Id, string Name, IReadOnlyList<Guid> EmployeeIds);

public sealed record TimesheetEmployeesDto(
    Guid? MyEmployeeId, IReadOnlyList<WorkEmployeeRefDto> Employees, IReadOnlyList<TimesheetProjectGroupDto> Projects,
    bool CanViewOthers, bool CanEditAll, bool CanApprove);

public sealed record TimesheetGroupDto(DateOnly From, DateOnly To, IReadOnlyList<WorkEmployeeRefDto> Employees, IReadOnlyList<TimesheetEntryDto> Entries);

public sealed record SaveTimesheetEntry(string? Date, string? ProjectId, string? TaskTypeId, JsonElement? Hours, string? Description);

public sealed record SaveTimesheetRequest(string? EmployeeId, IReadOnlyList<string?>? Dates, IReadOnlyList<SaveTimesheetEntry>? Entries);

public sealed record SaveTimesheetResult(IReadOnlyList<DateOnly> SavedDates, IReadOnlyList<DateOnly> SkippedDates);

public sealed record TimesheetApprovalRequest(string? From, string? To, string? Action, string? Reason, IReadOnlyList<string?>? EmployeeIds);

public sealed record TimesheetApprovalResult(int Updated);

/// <summary>
/// Work log (Nhật ký công việc): daily / weekly time entries, the team view and approval. The
/// rules engine (locks, hour limits) runs here on the server; the screen only shows its result.
/// Entries generated from approved leave / OT requests are read-only.
/// </summary>
public sealed class TimesheetService(IAppDbContext db, ICurrentUser user, WorkScope scope, RuleService rules, ITenantClock clock)
{
    /// <summary>Longest range one request may read (two months).</summary>
    private const int MaxRangeDays = 62;

    private const string ProjectTaskCode = "PROJECT";

    public async Task<TimesheetEmployeesDto> EmployeesAsync(CancellationToken ct)
    {
        var ids = await scope.VisibleEmployeeIdsAsync(ct);
        var vi = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: false);
        var employees = (await db.Set<Employee>().AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync(ct))
            .OrderBy(e => e.FullName, vi).Select(WorkScope.Ref).ToList();
        var assignments = await db.Set<ProjectAssignment>().AsNoTracking()
            .Where(a => !a.Project.IsDeleted && ids.Contains(a.EmployeeId))
            .Select(a => new { a.ProjectId, a.Project.Name, a.Project.CreatedAt, a.EmployeeId }).ToListAsync(ct);
        var projects = assignments.GroupBy(a => a.ProjectId)
            .OrderByDescending(g => g.First().CreatedAt)
            .Select(g => new TimesheetProjectGroupDto(g.Key, g.First().Name, g.Select(a => a.EmployeeId).Distinct().ToList())).ToList();
        return new TimesheetEmployeesDto(await scope.MyEmployeeIdAsync(ct), employees, projects,
            employees.Count > 1, user.Has(Permissions.TimesheetAllEdit), user.Has(Permissions.TimesheetApprove));
    }

    /// <summary>GET /timesheets?employeeId=&amp;from=&amp;to= — entries, day states, assigned projects, totals of the week of "from".</summary>
    public async Task<TimesheetDto> GetAsync(Guid? employeeIdParam, string? fromRaw, string? toRaw, CancellationToken ct)
    {
        var employeeId = employeeIdParam ?? await scope.RequireMyEmployeeIdAsync(ct);
        await scope.EnsureCanViewAsync(employeeId, ct);
        var (from, to) = ReadRange(fromRaw, toRaw);
        var set = await rules.SnapshotAsync(ct);
        var today = await clock.TodayAsync(ct);
        var culture = user.Culture;

        var weekStart = WorkDates.WeekStart(from);
        var loadFrom = from < weekStart ? from : weekStart;
        var loadTo = to > weekStart.AddDays(6) ? to : weekStart.AddDays(6);
        var all = await EntriesAsync([employeeId], loadFrom, loadTo, ct);
        var entries = all.Where(t => t.WorkDate >= from && t.WorkDate <= to).ToList();
        var holidays = await HolidaysAsync(from, to, ct);

        var taskTypes = await db.Set<MasterDatum>().AsNoTracking()
            .Where(m => m.Type == MasterDataTypes.TaskType && m.IsActive && !m.IsSystem && m.Code != ProjectTaskCode)
            .OrderBy(m => m.SortOrder).ToListAsync(ct);

        return new TimesheetDto(
            employeeId, from, to, await scope.CanEditTimesheetOfAsync(employeeId, ct), RulesOf(set),
            entries.Select(t => ToDto(t, culture)).ToList(),
            WorkDates.Range(from, to).Select(d =>
            {
                var (locked, reason, args) = DayLock(d, today, set);
                var approved = entries.Any(t => t.WorkDate == d && t.Status == "approved" && t.Source == TimesheetSources.Manual);
                return new TimesheetDayDto(d, locked, reason, args, approved, WorkDates.IsWeekend(d), holidays.GetValueOrDefault(d));
            }).ToList(),
            await AssignedProjectsAsync(employeeId, from, to, ct),
            new TimesheetWeekDto(weekStart, WorkDates.Range(weekStart, weekStart.AddDays(6)).ToDictionary(
                WorkDates.Iso, d => WorkDates.Round2(all.Where(t => t.WorkDate == d).Sum(t => t.Hours)))),
            taskTypes.Select(m => new TaskTypeRefDto(m.Id, m.Code, Localized.Name(culture, m.Name, m.NameEn))).ToList());
    }

    /// <summary>
    /// PUT /timesheets — saves several days at once: the manual entries of every editable day sent
    /// are replaced (locked days and days already approved are skipped). Checks:
    ///   hours 0–24 in steps of 0.25 h (15 minutes, as the day slider); 0 h rows are ignored
    ///   a project row needs an assignment covering that date (task type becomes PROJECT)
    ///   day total (incl. generated leave / OT rows) ≤ TIMESHEET_MAX_HOURS
    ///   week total (Mon–Sun) ≤ TIMESHEET_WEEKLY_MAX_HOURS, e.g. 5 × 12 h + 2 h = 62 h &gt; 60 h → error
    /// Saved rows are drafts waiting for approval.
    /// </summary>
    public async Task<(SaveTimesheetResult Result, int Days)> SaveAsync(SaveTimesheetRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var employeeId = string.IsNullOrWhiteSpace(request.EmployeeId)
            ? await scope.RequireMyEmployeeIdAsync(ct)
            : Guid.TryParse(request.EmployeeId, out var eid) ? eid : throw new ForbiddenException();
        if (!await scope.CanEditTimesheetOfAsync(employeeId, ct)) throw new ForbiddenException();

        var dates = (request.Dates ?? []).Select(WorkDates.Parse).OfType<DateOnly>().Distinct().ToList(); // bad strings are ignored
        if (dates.Count is 0 or > 31) throw AppValidationException.Single("dates", "validation.invalid_value");
        var set = await rules.SnapshotAsync(ct);
        var r = RulesOf(set);
        var today = await clock.TodayAsync(ct);
        var minDate = dates.Min();
        var maxDate = dates.Max();
        var all = await EntriesAsync([employeeId], WorkDates.WeekStart(minDate), WorkDates.WeekStart(maxDate).AddDays(6), ct);
        var editable = dates.Where(d => !DayLock(d, today, set).Locked
            && !all.Any(t => t.WorkDate == d && t.Status == "approved" && t.Source == TimesheetSources.Manual)).ToList();

        var projectTask = await db.Set<MasterDatum>().AsNoTracking().FirstOrDefaultAsync(m => m.Type == MasterDataTypes.TaskType && m.Code == ProjectTaskCode, ct);
        var taskTypes = await db.Set<MasterDatum>().AsNoTracking().Where(m => m.Type == MasterDataTypes.TaskType)
            .ToDictionaryAsync(m => m.Id, m => m.IsSystem, ct);
        var assigned = await AssignedProjectsAsync(employeeId, minDate, maxDate, ct);
        var clean = new List<(DateOnly Date, Guid? ProjectId, Guid TaskTypeId, decimal Hours, string? Description)>();
        var rows = request.Entries ?? [];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (WorkDates.Parse(row.Date) is not { } date || !dates.Contains(date)) continue;
            decimal hours;
            if (row.Hours is { ValueKind: JsonValueKind.Number } h && h.TryGetDecimal(out var hv)) hours = hv;
            else if (row.Hours is { ValueKind: JsonValueKind.String } hs && decimal.TryParse(hs.GetString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var hp)) hours = hp;
            else { errors.Add($"entries.{i}", "validation.invalid_number"); continue; }
            if (hours is < 0 or > 24) { errors.Add($"entries.{i}", "validation.invalid_number"); continue; }
            if (hours * 4 != decimal.Truncate(hours * 4)) { errors.Add($"entries.{i}", "timesheet_save.err_quarter_hour"); continue; }
            if (hours == 0) continue;

            Guid? projectId = Guid.TryParse(row.ProjectId, out var pid) ? pid : null;
            Guid taskTypeId;
            if (projectId is { } p)
            {
                var project = assigned.FirstOrDefault(a => a.Id == p);
                if (project is null || !project.Ranges.Any(rg => rg.Start <= date && rg.End >= date)) { errors.Add(WorkDates.Iso(date), "timesheet_save.err_no_assignment"); continue; }
                if (projectTask is null) { errors.Add($"entries.{i}", "validation.invalid_value"); continue; }
                taskTypeId = projectTask.Id;
            }
            else
            {
                if (!Guid.TryParse(row.TaskTypeId, out taskTypeId) || !taskTypes.TryGetValue(taskTypeId, out var isSystem) || isSystem)
                { errors.Add($"entries.{i}", "validation.invalid_value"); continue; }
            }
            var description = Text.Clean(row.Description);
            clean.Add((date, projectId, taskTypeId, hours, description.Length == 0 ? null : description[..Math.Min(500, description.Length)]));
        }

        decimal DayTotal(DateOnly d) => editable.Contains(d)
            ? clean.Where(c => c.Date == d).Sum(c => c.Hours) + all.Where(t => t.WorkDate == d && t.Source != TimesheetSources.Manual).Sum(t => t.Hours)
            : all.Where(t => t.WorkDate == d).Sum(t => t.Hours);
        foreach (var d in editable)
            if (DayTotal(d) > r.MaxHours) errors.Add(WorkDates.Iso(d), "timesheet_save.err_max_hours", new() { ["hours"] = r.MaxHours });
        if (r.WeeklyMaxHours is { } weekly)
        {
            foreach (var ws in editable.Select(WorkDates.WeekStart).Distinct())
            {
                var total = WorkDates.Range(ws, ws.AddDays(6)).Sum(DayTotal);
                if (total > weekly) errors.Add(WorkDates.Iso(ws), "timesheet_save.err_weekly_max_hours", new() { ["hours"] = weekly, ["total"] = WorkDates.Round2(total) });
            }
        }
        errors.ThrowIfAny();
        if (editable.Count == 0) throw new ConflictException("timesheet_save.err_all_locked");

        var old = await db.Set<TimesheetEntry>()
            .Where(t => t.EmployeeId == employeeId && editable.Contains(t.WorkDate) && t.Source == TimesheetSources.Manual).ToListAsync(ct);
        db.Set<TimesheetEntry>().RemoveRange(old);
        foreach (var c in clean.Where(c => editable.Contains(c.Date)))
        {
            db.Set<TimesheetEntry>().Add(new TimesheetEntry
            {
                EmployeeId = employeeId, ProjectId = c.ProjectId, TaskTypeId = c.TaskTypeId, WorkDate = c.Date, Hours = c.Hours,
                Description = c.Description, Status = "draft", Source = TimesheetSources.Manual,
            });
        }
        await db.SaveChangesAsync(ct);
        return (new SaveTimesheetResult(editable, dates.Where(d => !editable.Contains(d)).ToList()), editable.Count);
    }

    /// <summary>GET /timesheets/group?employeeIds=&amp;from=&amp;to= — several people (only those the user may see).</summary>
    public async Task<TimesheetGroupDto> GroupAsync(IReadOnlyCollection<Guid> employeeIds, string? fromRaw, string? toRaw, CancellationToken ct)
    {
        var visible = await scope.VisibleEmployeeIdsAsync(ct);
        var ids = employeeIds.Where(visible.Contains).ToList();
        var (from, to) = ReadRange(fromRaw, toRaw);
        var employees = await db.Set<Employee>().AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        var culture = user.Culture;
        return new TimesheetGroupDto(from, to, ids.Where(employees.ContainsKey).Select(id => WorkScope.Ref(employees[id])).ToList(),
            (await EntriesAsync(ids, from, to, ct)).Select(t => ToDto(t, culture)).ToList());
    }

    /// <summary>
    /// POST /timesheets/approval — approves / rejects the draft manual entries of the chosen
    /// people in [from, to]. Returns the message key and the number of people.
    /// </summary>
    public async Task<(TimesheetApprovalResult Result, string Key, int People)> ApproveAsync(TimesheetApprovalRequest r, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var from = errors.Date("from", r.From);
        var to = errors.Date("to", r.To);
        var action = Text.Clean(r.Action);
        if (action is not ("approve" or "reject")) errors.Add("action", "validation.invalid_value");
        var reason = Text.Clean(r.Reason);
        if (action == "reject" && reason.Length == 0) errors.Add("reason", "request_decide.err_reason_required");
        errors.MaxLength("reason", reason, 500);
        errors.ThrowIfAny();

        var visible = await scope.VisibleEmployeeIdsAsync(ct);
        var ids = (r.EmployeeIds ?? []).Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(visible.Contains).Distinct().ToList();
        var rows = await db.Set<TimesheetEntry>()
            .Where(t => ids.Contains(t.EmployeeId) && t.Source == TimesheetSources.Manual && t.Status == "draft" && t.WorkDate >= from && t.WorkDate <= to)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var t in rows)
        {
            t.Status = action == "approve" ? "approved" : "rejected";
            t.RejectionReason = action == "reject" ? reason : null;
            t.ApprovedBy = user.UserId;
            t.ApprovedAt = now;
        }
        await db.SaveChangesAsync(ct);
        if (rows.Count == 0) return (new TimesheetApprovalResult(0), "timesheet_approval_empty.msg_success", 0);
        return (new TimesheetApprovalResult(rows.Count), action == "approve" ? "timesheet_approve.msg_success" : "timesheet_reject.msg_success", ids.Count);
    }

    // ─── rules engine ────────────────────────────────────────────────────────

    public static TimesheetRulesDto RulesOf(RuleSet set) => new(
        set.Enabled(RuleKeys.TimesheetMinHours) ? set.Number(RuleKeys.TimesheetMinHours, 0) : 0,
        set.Enabled(RuleKeys.TimesheetMaxHours) ? set.Number(RuleKeys.TimesheetMaxHours, 24) : 24,
        set.Enabled(RuleKeys.TimesheetWeeklyMaxHours) ? set.Number(RuleKeys.TimesheetWeeklyMaxHours, 60) : null,
        set.Number(RuleKeys.TimesheetFteHours, 8));

    /// <summary>
    /// Whether a day can still be edited (port of the original getUIState), first match wins:
    ///   edit_limit   — more than TIMESHEET_EDIT_LIMIT_DAYS days ago (e.g. 7: on the 20th, the 12th is locked)
    ///   month_locked — a month before the previous one, or the previous month once today's day of
    ///                  month is past TIMESHEET_LOCK_DATE_OF_MONTH (e.g. 5: September closes on 6 October)
    ///   future       — after today, unless TIMESHEET_ALLOW_FUTURE is on
    /// reason is the FE label suffix timesheet.lbl_lock_&lt;reason&gt;; args fill its placeholders.
    /// </summary>
    public static (bool Locked, string? Reason, IReadOnlyDictionary<string, object?> Args) DayLock(DateOnly day, DateOnly today, RuleSet set)
    {
        var diffDays = today.DayNumber - day.DayNumber;
        if (set.Enabled(RuleKeys.TimesheetEditLimitDays))
        {
            var max = set.Number(RuleKeys.TimesheetEditLimitDays, 14);
            if (diffDays > max) return (true, "edit_limit", new Dictionary<string, object?> { ["days"] = max });
        }
        if (set.Enabled(RuleKeys.TimesheetLockDateOfMonth))
        {
            var lockDay = set.Number(RuleKeys.TimesheetLockDateOfMonth, 5);
            var diffMonths = today.Year * 12 + today.Month - (day.Year * 12 + day.Month);
            if (diffMonths > 1 || (diffMonths == 1 && today.Day > lockDay)) return (true, "month_locked", new Dictionary<string, object?> { ["day"] = lockDay });
        }
        var futureOk = set.Enabled(RuleKeys.TimesheetAllowFuture) && set.Bool(RuleKeys.TimesheetAllowFuture, false);
        if (day > today && !futureOk) return (true, "future", new Dictionary<string, object?>());
        return (false, null, new Dictionary<string, object?>());
    }

    // ─── queries ─────────────────────────────────────────────────────────────

    private static (DateOnly From, DateOnly To) ReadRange(string? fromRaw, string? toRaw)
    {
        var errors = new ValidationErrors();
        var from = errors.Date("from", fromRaw);
        var to = errors.Date("to", toRaw);
        if (from is { } f && to is { } t && (t < f || t.DayNumber - f.DayNumber + 1 > MaxRangeDays)) errors.Add("to", "validation.invalid_value");
        errors.ThrowIfAny();
        return (from!.Value, to!.Value);
    }

    /// <summary>Entries of the people in [from, to], leaving out those of projects in the trash.</summary>
    internal async Task<List<TimesheetEntry>> EntriesAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to, CancellationToken ct)
        => await db.Set<TimesheetEntry>().AsNoTracking().Include(t => t.Project).Include(t => t.TaskType)
            .Where(t => employeeIds.Contains(t.EmployeeId) && t.WorkDate >= from && t.WorkDate <= to && (t.ProjectId == null || !t.Project!.IsDeleted))
            .OrderBy(t => t.WorkDate).ThenBy(t => t.CreatedAt).ThenBy(t => t.Id).ToListAsync(ct);

    internal async Task<Dictionary<DateOnly, string>> HolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct)
        => await db.Set<Holiday>().AsNoTracking().Where(h => h.HolidayDate >= from && h.HolidayDate <= to).ToDictionaryAsync(h => h.HolidayDate, h => h.Name, ct);

    /// <summary>Projects the person is assigned to in [from, to] with the assignment ranges (default 0 h rows + date checks).</summary>
    internal async Task<List<AssignedProjectDto>> AssignedProjectsAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.Set<ProjectAssignment>().AsNoTracking().Include(a => a.Project).ThenInclude(p => p.Status)
            .Where(a => a.EmployeeId == employeeId && a.StartDate <= to && a.EndDate >= from && !a.Project.IsDeleted)
            .OrderBy(a => a.CreatedAt).ToListAsync(ct);
        var culture = user.Culture;
        var vi = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: false);
        return rows.GroupBy(a => a.ProjectId).Select(g =>
        {
            var p = g.First().Project;
            return new AssignedProjectDto(p.Id, p.Code, p.Name, MasterDataService.Ref(p.Status, culture),
                g.Select(a => new DateRangeDto(a.StartDate, a.EndDate)).ToList());
        }).OrderBy(p => p.Name, vi).ToList();
    }

    internal static TimesheetEntryDto ToDto(TimesheetEntry t, string culture) => new(
        t.Id, t.WorkDate, t.ProjectId, t.Project?.Name,
        t.TaskType is null ? null : new TaskTypeRefDto(t.TaskType.Id, t.TaskType.Code, Localized.Name(culture, t.TaskType.Name, t.TaskType.NameEn)),
        t.Hours, t.Description, t.Status, TimesheetSources.ToApi(t.Source), t.RejectionReason, t.EmployeeId);
}
