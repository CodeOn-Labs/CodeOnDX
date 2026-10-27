using System.Text.Json;
using Dx.Application.Common;
using Dx.Application.MasterData;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Projects;

public sealed record StaffEmployeeDto(
    Guid Id, string Code, string FullName, string? AvatarUrl, string? Position, DateOnly? LeftDate,
    int AllocationPercent, bool Overloaded, decimal OverloadThreshold, bool Resigned, bool LeavingSoon);

public sealed record AssignableEmployeeDto(
    Guid Id, string Code, string FullName, string? AvatarUrl, string? Position, DateOnly? LeftDate,
    int AllocationPercent, bool Overloaded, decimal OverloadThreshold, bool Resigned, bool LeavingSoon, string? Level);

public sealed record FteSumsDto(decimal Contract, decimal Planned, decimal Actual);

public sealed record StaffIntervalDto(Guid Id, DateOnly StartDate, DateOnly EndDate, decimal Fte, FteSumsDto Sums);

public sealed record StaffAssignmentDto(
    Guid Id, string RoleInProject, StaffEmployeeDto? Employee, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<StaffIntervalDto> Intervals);

public sealed record FtePointDto(DateOnly Date, decimal Contract, decimal Planned, decimal Actual);

public sealed record StaffSlotDto(
    string Key, Guid? ContractStaffId, int SlotIndex, decimal ContractFte, DateOnly? ContractStartDate, DateOnly? ContractEndDate,
    IReadOnlyList<StaffAssignmentDto> Assignments, IReadOnlyList<FtePointDto> Series);

public sealed record StaffGroupDto(
    string Key, string RoleCode, string RoleName, MasterRefDto? Level, int ContractCount, IReadOnlyList<StaffSlotDto> Slots, int AssignedCount);

public sealed record StaffingDto(DateOnly StartDate, DateOnly EndDate, decimal FteHours, IReadOnlyList<StaffGroupDto> Groups);

public sealed record IntervalRequest(string? StartDate, string? EndDate, JsonElement? Fte);

public sealed record SaveAssignmentRequest(
    string? EmployeeId, string? ContractStaffId, JsonElement? SlotIndex, string? RoleInProject, string? Mode, string? HandoverDate,
    IReadOnlyList<IntervalRequest>? Intervals);

/// <summary>
/// Project staffing (Overview tab): contract positions (slots), who fills each slot over time and
/// the daily contract / planned / actual FTE lines. An assignment = one person in one slot, with
/// one or more periods (project_assignment_periods) of different FTE; the assignment row keeps the
/// outer range and the highest FTE so summary screens read it quickly.
/// </summary>
public sealed class ProjectStaffingService(IAppDbContext db, ICurrentUser user, ProjectAccess access, RuleService rules, ITenantClock clock)
{
    public const string ModeUpdate = "update";
    public const string ModeHandover = "handover";
    public const string ModeReplace = "replace";

    /// <summary>Longest chart range (3 years).</summary>
    private const int MaxDays = 1095;

    /// <summary>
    /// GET /projects/{id}/staffing. Range = project dates (no end → start + 179 days). Per slot and day:
    ///   contract = fte_per_person of the contract position when the day is inside its dates, else 0
    ///   planned  = highest FTE among the holder's periods active that day (0 when the slot is empty)
    ///   actual   = hours the holder logged on the project that day ÷ TIMESHEET_FTE_HOURS
    ///              (a weekend without log repeats the last weekday so the line does not drop to 0)
    /// Period sums: contract / planned add Monday–Friday values only (FTE-days); actual adds every day.
    /// Example: 1 FTE contract over a week = 5; 6 h logged per weekday with 8 h per FTE → actual 0.75 a day.
    /// A user without project.manage only sees the slots they hold.
    /// </summary>
    public async Task<StaffingDto> StaffingAsync(Guid projectId, CancellationToken ct)
    {
        var scope = await access.OpenAsync(projectId, ct);
        var p = scope.Project;
        var today = await clock.TodayAsync(ct);
        var start = p.StartDate ?? today;
        var end = p.EndDate is { } e && e >= start ? e : start.AddDays(179);
        var dayCount = Math.Min(MaxDays, end.DayNumber - start.DayNumber + 1);
        var days = Enumerable.Range(0, dayCount).Select(start.AddDays).ToList();
        var perFte = await rules.GetNumberAsync(RuleKeys.TimesheetFteHours, 8, ct);
        var culture = user.Culture;

        var hoursBy = (await db.Set<TimesheetEntry>().AsNoTracking().Where(t => t.ProjectId == p.Id)
                .GroupBy(t => new { t.EmployeeId, t.WorkDate }).Select(g => new { g.Key.EmployeeId, g.Key.WorkDate, Hours = g.Sum(t => t.Hours) })
                .ToListAsync(ct))
            .ToDictionary(x => (x.EmployeeId, x.WorkDate), x => x.Hours);
        var staff = await db.Set<ProjectContractStaff>().AsNoTracking().Include(s => s.Position).Include(s => s.Level)
            .Where(s => s.ProjectId == p.Id).OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToListAsync(ct);
        var assignments = await db.Set<ProjectAssignment>().AsNoTracking()
            .Include(a => a.Position).Include(a => a.Level).Include(a => a.ProjectAssignmentPeriods)
            .Where(a => a.ProjectId == p.Id).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync(ct);
        var refs = await EmployeeRefsAsync(assignments.Select(a => a.EmployeeId).Distinct().ToList(), today, ct);

        StaffSlotDto Slot(string key, ProjectContractStaff? cs, int slotIndex, IReadOnlyList<ProjectAssignment> rows)
        {
            var contractFte = cs?.FtePerPerson ?? 0;
            var intervals = rows.SelectMany(a => a.ProjectAssignmentPeriods.Select(s => (a, s))).ToList();
            var series = new List<FtePointDto>(days.Count);
            decimal lastWorkdayActual = 0;
            foreach (var d in days)
            {
                var active = intervals.Where(x => x.s.StartDate <= d && x.s.EndDate >= d).ToList();
                var contract = cs is not null && cs.StartDate <= d && cs.EndDate >= d ? contractFte : 0;
                var planned = active.Count == 0 ? 0 : active.Max(x => x.s.FteAllocated);
                var hours = active.Sum(x => hoursBy.GetValueOrDefault((x.a.EmployeeId, d)));
                var actual = active.Count > 0 && hours > 0 && perFte > 0 ? ProjectDates.Round2(hours / perFte) : 0;
                if (active.Count > 0 && hours == 0 && ProjectDates.IsWeekend(d)) actual = lastWorkdayActual;
                if (!ProjectDates.IsWeekend(d)) lastWorkdayActual = actual;
                series.Add(new FtePointDto(d, ProjectDates.Round2(contract), ProjectDates.Round2(planned), actual));
            }
            FteSumsDto Sums(DateOnly from, DateOnly to)
            {
                var inRange = series.Where(pt => pt.Date >= from && pt.Date <= to).ToList();
                var work = inRange.Where(pt => !ProjectDates.IsWeekend(pt.Date)).ToList();
                return new FteSumsDto(ProjectDates.Round2(work.Sum(pt => pt.Contract)), ProjectDates.Round2(work.Sum(pt => pt.Planned)),
                    ProjectDates.Round2(inRange.Sum(pt => pt.Actual)));
            }
            return new StaffSlotDto(key, cs?.Id, slotIndex, contractFte, cs?.StartDate, cs?.EndDate,
                rows.Select(a =>
                {
                    var periods = a.ProjectAssignmentPeriods.OrderBy(s => s.StartDate).ToList();
                    var last = periods.LastOrDefault()?.EndDate ?? a.EndDate;
                    return new StaffAssignmentDto(a.Id, a.Position.Code, RefWithLeave(refs.GetValueOrDefault(a.EmployeeId), last, today),
                        periods.FirstOrDefault()?.StartDate ?? a.StartDate, last,
                        periods.Select(s => new StaffIntervalDto(s.Id, s.StartDate, s.EndDate, s.FteAllocated, Sums(s.StartDate, s.EndDate))).ToList());
                }).ToList(),
                series);
        }

        var groups = new List<(string Key, string RoleCode, string RoleName, MasterRefDto? Level, int Count, List<StaffSlotDto> Slots)>();
        (string Key, string RoleCode, string RoleName, MasterRefDto? Level, int Count, List<StaffSlotDto> Slots) GroupFor(string key, MasterDatum position, MasterDatum? level)
        {
            var i = groups.FindIndex(g => g.Key == key);
            if (i < 0)
            {
                groups.Add((key, position.Code, Localized.Name(culture, position.Name, position.NameEn), MasterDataService.Ref(level, culture), 0, []));
                i = groups.Count - 1;
            }
            return groups[i];
        }
        foreach (var cs in staff)
        {
            var key = $"{cs.Position.Code}::{cs.LevelId?.ToString() ?? "null"}";
            var g = GroupFor(key, cs.Position, cs.Level);
            var index = groups.FindIndex(x => x.Key == key);
            groups[index] = g with { Count = g.Count + cs.Quantity };
            for (var i = 0; i < cs.Quantity; i++)
            {
                var slotIndex = i;
                g.Slots.Add(Slot($"{cs.Id}:{i}", cs, i, assignments.Where(a => a.ContractStaffId == cs.Id && a.SlotIndex == slotIndex).ToList()));
            }
        }
        // Assignments outside the contract → group "outside" per position.
        foreach (var a in assignments.Where(a => a.ContractStaffId is null || staff.All(cs => cs.Id != a.ContractStaffId)))
        {
            var g = GroupFor($"outside::{a.Position.Code}", a.Position, a.Level);
            g.Slots.Add(Slot($"outside:{a.Id}", null, 0, [a]));
        }

        var result = groups.Select(g => new StaffGroupDto(g.Key, g.RoleCode, g.RoleName, g.Level, g.Count, g.Slots, g.Slots.Count(s => s.Assignments.Count > 0))).ToList();
        if (!scope.CanManage)
        {
            var me = scope.EmployeeId;
            result = result
                .Select(g => g with { Slots = g.Slots.Where(s => s.Assignments.Any(a => a.Employee?.Id == me)).ToList() })
                .Where(g => g.Slots.Count > 0).ToList();
        }
        return new StaffingDto(start, end, perFte, result);
    }

    /// <summary>GET /projects/{id}/assignable-employees — working people (not deleted, not resigned, no past leave date), by name.</summary>
    public async Task<IReadOnlyList<AssignableEmployeeDto>> AssignableAsync(Guid projectId, CancellationToken ct)
    {
        await access.ManageAsync(projectId, ct);
        var today = await clock.TodayAsync(ct);
        var ids = await db.Set<Employee>().AsNoTracking()
            .Where(e => e.Status.Code != MasterDataCodes.EmployeeStatusResigned && (e.TerminationDate == null || e.TerminationDate > today))
            .Select(e => e.Id).ToListAsync(ct);
        var refs = await EmployeeRefsAsync(ids, today, ct);
        var vi = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: false);
        return refs.Values.OrderBy(r => r.Dto.FullName, vi).Select(r =>
        {
            var d = RefWithLeave(r, null, today)!;
            return new AssignableEmployeeDto(d.Id, d.Code, d.FullName, d.AvatarUrl, d.Position, d.LeftDate, d.AllocationPercent,
                d.Overloaded, d.OverloadThreshold, d.Resigned, d.LeavingSoon, r.LevelName);
        }).ToList();
    }

    /// <summary>POST /projects/{id}/assignments — one person holds one position per project. Returns the employee name.</summary>
    public async Task<string> CreateAsync(Guid projectId, SaveAssignmentRequest r, CancellationToken ct)
    {
        var scope = await access.ManageAsync(projectId, ct);
        var p = scope.Project;
        var (errors, intervals) = ParseIntervals(r, p);
        var employeeId = errors.Id("employeeId", r.EmployeeId);
        Employee? employee = null;
        if (string.IsNullOrWhiteSpace(r.EmployeeId)) errors.Add("employeeId", "validation.required");
        else if (employeeId is { } eid && (employee = await db.Set<Employee>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == eid, ct)) is null)
            errors.Add("employeeId", "validation.invalid_value");
        var csId = errors.Id("contractStaffId", r.ContractStaffId);
        ProjectContractStaff? cs = null;
        if (csId is { } c && (cs = await db.Set<ProjectContractStaff>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == c && x.ProjectId == p.Id, ct)) is null)
            errors.Add("contractStaffId", "validation.invalid_value");
        var slotIndex = 0;
        if (cs is not null)
        {
            slotIndex = r.SlotIndex is { ValueKind: JsonValueKind.Number } si && si.TryGetInt32(out var n) ? n : r.SlotIndex is null ? 0 : -1;
            if (slotIndex < 0 || slotIndex >= cs.Quantity) errors.Add("slotIndex", "validation.invalid_value");
        }
        Guid? positionId = cs?.PositionId;
        if (cs is null)
        {
            var code = Text.Clean(r.RoleInProject);
            positionId = await db.Set<MasterDatum>().AsNoTracking().Where(m => m.Type == MasterDataTypes.JobPosition && m.Code == code).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
            if (positionId is null) errors.Add("roleInProject", "validation.required");
        }
        errors.ThrowIfAny();

        if (await db.Set<ProjectAssignment>().AnyAsync(a => a.ProjectId == p.Id && a.EmployeeId == employee!.Id, ct))
            throw new ConflictException("project_assignment.err_already_assigned");
        var a = NewAssignment(p.Id, employee!.Id, cs?.Id, slotIndex, positionId!.Value, cs?.LevelId ?? employee.LevelId);
        await WriteAsync(a, intervals, ct);
        await db.SaveChangesAsync(ct);
        return employee.FullName;
    }

    /// <summary>
    /// PUT /projects/{id}/assignments/{assignmentId}. Same person or mode "update": the periods are
    /// replaced (and the person swapped in place). Another person with mode
    ///   "replace"  — wrong entry: the old person's history in this slot is removed
    ///   "handover" — the old person works until handoverDate, the new one starts the next day;
    ///                old periods after that date are cut, new periods before it are moved
    /// Returns (message key, employee name).
    /// </summary>
    public async Task<(string Key, string Name)> UpdateAsync(Guid projectId, Guid assignmentId, SaveAssignmentRequest r, CancellationToken ct)
    {
        var scope = await access.ManageAsync(projectId, ct);
        var p = scope.Project;
        var a = await db.Set<ProjectAssignment>().Include(x => x.ProjectAssignmentPeriods)
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.ProjectId == p.Id, ct) ?? throw new NotFoundException();
        var (errors, intervals) = ParseIntervals(r, p);
        var employeeId = string.IsNullOrWhiteSpace(r.EmployeeId) ? a.EmployeeId : errors.Id("employeeId", r.EmployeeId) ?? Guid.Empty;
        var employee = await db.Set<Employee>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) errors.Add("employeeId", "validation.invalid_value");
        var mode = r.Mode is ModeUpdate or ModeHandover or ModeReplace ? r.Mode : ModeUpdate;
        DateOnly? handover = null;
        if (mode == ModeHandover && employeeId != a.EmployeeId)
        {
            handover = errors.Date("handoverDate", r.HandoverDate);
            var firstStart = a.ProjectAssignmentPeriods.Select(s => (DateOnly?)s.StartDate).Min() ?? a.StartDate;
            if (handover is { } h && h < firstStart) errors.Add("handoverDate", "project_assignment.err_handover_before_start");
        }
        errors.ThrowIfAny();

        if (employeeId != a.EmployeeId && await db.Set<ProjectAssignment>().AnyAsync(x => x.ProjectId == p.Id && x.EmployeeId == employeeId, ct))
            throw new ConflictException("project_assignment.err_already_assigned");
        if (employeeId == a.EmployeeId || mode == ModeUpdate)
        {
            a.EmployeeId = employeeId;
            await WriteAsync(a, intervals, ct);
            await db.SaveChangesAsync(ct);
            return ("project_assignment_update.msg_success", employee!.FullName);
        }
        if (mode == ModeReplace) SoftDelete(a);
        else
        {
            var h = handover!.Value;
            var kept = a.ProjectAssignmentPeriods.Where(s => s.StartDate <= h)
                .Select(s => new Interval(s.StartDate, s.EndDate > h ? h : s.EndDate, s.FteAllocated)).ToList();
            if (kept.Count > 0) await WriteAsync(a, kept, ct);
            else SoftDelete(a);
            var from = h.AddDays(1);
            intervals = intervals.Select(it => it.Start < from ? it with { Start = from } : it).Where(it => it.End >= it.Start).ToList();
            if (intervals.Count == 0) throw AppValidationException.Single("handoverDate", "project_assignment.err_handover_after_end");
        }
        var b = NewAssignment(p.Id, employeeId, a.ContractStaffId, a.SlotIndex, a.PositionId, a.LevelId);
        await WriteAsync(b, intervals, ct);
        await db.SaveChangesAsync(ct);
        return (mode == ModeHandover ? "project_assignment_handover.msg_success" : "project_assignment_update.msg_success", employee!.FullName);
    }

    /// <summary>DELETE /projects/{id}/assignments/{assignmentId}?intervalId= — one period, or the whole assignment. Logged time stays.</summary>
    public async Task<string> DeleteAsync(Guid projectId, Guid assignmentId, Guid? intervalId, CancellationToken ct)
    {
        var scope = await access.ManageAsync(projectId, ct);
        var a = await db.Set<ProjectAssignment>().Include(x => x.ProjectAssignmentPeriods).Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.ProjectId == scope.Project.Id, ct) ?? throw new NotFoundException();
        var rest = intervalId is { } iid ? a.ProjectAssignmentPeriods.Where(s => s.Id != iid).Select(s => new Interval(s.StartDate, s.EndDate, s.FteAllocated)).ToList() : [];
        if (rest.Count > 0) await WriteAsync(a, rest, ct);
        else SoftDelete(a);
        await db.SaveChangesAsync(ct);
        return a.Employee.FullName;
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private sealed record Interval(DateOnly Start, DateOnly End, decimal Fte);

    private sealed record EmployeeRef(StaffEmployeeDto Dto, bool IsActive, string? LevelName);

    /// <summary>
    /// Employee card used in staffing: allocationPercent = Σ FTE of the periods active today on
    /// every project not in the trash × 100 (rounded); overloaded when above HR_OVERLOAD_ALLOCATION_THRESHOLD.
    /// </summary>
    private async Task<Dictionary<Guid, EmployeeRef>> EmployeeRefsAsync(IReadOnlyCollection<Guid> ids, DateOnly today, CancellationToken ct)
    {
        var overload = await rules.GetNumberAsync(RuleKeys.HrOverloadAllocationThreshold, 100, ct);
        var employees = await db.Set<Employee>().IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking()
            .Include(e => e.Position).Include(e => e.Level).Include(e => e.Status)
            .Where(e => ids.Contains(e.Id)).ToListAsync(ct);
        var fte = (await db.Set<ProjectAssignmentPeriod>().AsNoTracking()
                .Where(s => ids.Contains(s.Assignment.EmployeeId) && !s.Assignment.IsDeleted && !s.Assignment.Project.IsDeleted
                            && s.StartDate <= today && s.EndDate >= today)
                .Select(s => new { s.Assignment.EmployeeId, s.FteAllocated }).ToListAsync(ct))
            .GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(x => x.FteAllocated));
        var culture = user.Culture;
        return employees.ToDictionary(e => e.Id, e =>
        {
            var percent = (int)Math.Round(fte.GetValueOrDefault(e.Id) * 100, MidpointRounding.AwayFromZero);
            var position = e.Position is null ? null : Localized.Name(culture, e.Position.Name, e.Position.NameEn);
            return new EmployeeRef(
                new StaffEmployeeDto(e.Id, e.Code, e.FullName, e.AvatarUrl, position, e.TerminationDate, percent, percent > overload, overload, false, false),
                !e.IsDeleted && e.Status.Code != MasterDataCodes.EmployeeStatusResigned,
                e.Level is null ? null : Localized.Name(culture, e.Level.Name, e.Level.NameEn));
        });
    }

    private static StaffEmployeeDto? RefWithLeave(EmployeeRef? r, DateOnly? assignmentEnd, DateOnly today)
    {
        if (r is null) return null;
        var (resigned, leavingSoon) = ProjectDetailService.LeaveFlags(r.IsActive, r.Dto.LeftDate, assignmentEnd, today);
        return r.Dto with { Resigned = resigned, LeavingSoon = leavingSoon };
    }

    /// <summary>
    /// Periods of the request: at least one; dates inside the project; FTE 0.01–2; periods of the
    /// same person must not overlap — A and B overlap when startA ≤ endB and endA ≥ startB.
    /// </summary>
    private static (ValidationErrors Errors, List<Interval> Intervals) ParseIntervals(SaveAssignmentRequest r, Project p)
    {
        var errors = new ValidationErrors();
        var list = r.Intervals ?? [];
        if (list.Count == 0) errors.Add("intervals", "project_assignment.err_interval_required");
        var parsed = new List<(DateOnly? S, DateOnly? E, decimal? Fte)>();
        for (var i = 0; i < list.Count; i++)
        {
            string F(string k) => $"intervals.{i}.{k}";
            var s = errors.Date(F("startDate"), list[i].StartDate);
            var e = errors.Date(F("endDate"), list[i].EndDate);
            var fte = errors.Number(F("fte"), list[i].Fte, 0.01m, 2);
            if (s is { } s1 && e is { } e1 && e1 < s1) errors.Add(F("endDate"), "project_save.err_end_before_start");
            if (s is { } s2 && p.StartDate is { } ps && s2 < ps) errors.Add(F("startDate"), "project_assignment.err_before_project", new() { ["date"] = ps.ToString("yyyy-MM-dd") });
            if (e is { } e2 && p.EndDate is { } pe && e2 > pe) errors.Add(F("endDate"), "project_assignment.err_after_project", new() { ["date"] = pe.ToString("yyyy-MM-dd") });
            parsed.Add((s, e, fte));
        }
        for (var i = 0; i < parsed.Count; i++)
            for (var j = i + 1; j < parsed.Count; j++)
                if (parsed[i] is { S: { } aS, E: { } aE } && parsed[j] is { S: { } bS, E: { } bE } && aS <= bE && aE >= bS)
                    errors.Add($"intervals.{j}.startDate", "project_assignment.err_overlap", new() { ["a"] = i + 1, ["b"] = j + 1 });
        var intervals = errors.Any ? [] : parsed.Select(x => new Interval(x.S!.Value, x.E!.Value, x.Fte!.Value)).ToList();
        return (errors, intervals);
    }

    private ProjectAssignment NewAssignment(Guid projectId, Guid employeeId, Guid? contractStaffId, int slotIndex, Guid positionId, Guid? levelId)
    {
        var a = new ProjectAssignment
        {
            Id = Guid.CreateVersion7(), ProjectId = projectId, EmployeeId = employeeId, ContractStaffId = contractStaffId,
            SlotIndex = slotIndex, PositionId = positionId, LevelId = levelId,
        };
        db.Set<ProjectAssignment>().Add(a);
        return a;
    }

    /// <summary>Replaces the periods; the assignment keeps outer range + highest FTE; the person becomes a project member.</summary>
    private async Task WriteAsync(ProjectAssignment a, IReadOnlyList<Interval> intervals, CancellationToken ct)
    {
        foreach (var old in a.ProjectAssignmentPeriods.ToList()) db.Set<ProjectAssignmentPeriod>().Remove(old);
        a.ProjectAssignmentPeriods.Clear();
        var sorted = intervals.OrderBy(x => x.Start).ToList();
        foreach (var it in sorted)
            a.ProjectAssignmentPeriods.Add(new ProjectAssignmentPeriod { AssignmentId = a.Id, StartDate = it.Start, EndDate = it.End, FteAllocated = it.Fte });
        a.StartDate = sorted[0].Start;
        a.EndDate = sorted[^1].End;
        a.FteAllocated = sorted.Max(x => x.Fte);
        var isMember = await db.Set<ProjectMember>().AnyAsync(m => m.ProjectId == a.ProjectId && m.EmployeeId == a.EmployeeId, ct)
                       || db.Set<ProjectMember>().Local.Any(m => m.ProjectId == a.ProjectId && m.EmployeeId == a.EmployeeId);
        if (!isMember) db.Set<ProjectMember>().Add(new ProjectMember { ProjectId = a.ProjectId, EmployeeId = a.EmployeeId, PositionId = a.PositionId });
    }

    private void SoftDelete(ProjectAssignment a)
    {
        a.IsDeleted = true;
        a.DeletedAt = DateTime.UtcNow;
        a.DeletedBy = user.UserId;
    }
}
