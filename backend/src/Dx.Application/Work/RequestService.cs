using System.Text.Json;
using Dx.Application.Common;
using Dx.Application.MasterData;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Work;

public sealed record ApproverRefDto(Guid Id, string FullName);

public sealed record ProjectNameRefDto(Guid Id, string Name);

public sealed record LeaveRequestDto(
    Guid Id, string Code, WorkEmployeeRefDto? Employee, string? Reason, string Status, ApproverRefDto? Approver, DateTime? DecidedAt,
    string? DecisionNote, DateTime CreatedAt, MasterRefDto? LeaveType, DateOnly FromDate, DateOnly ToDate, string DurationType,
    decimal Days, decimal TotalHours, string? AttachmentUrl);

public sealed record OtRequestDto(
    Guid Id, string Code, WorkEmployeeRefDto? Employee, string? Reason, string Status, ApproverRefDto? Approver, DateTime? DecidedAt,
    string? DecisionNote, DateTime CreatedAt, ProjectNameRefDto? Project, DateOnly WorkDate, string StartTime, string EndTime,
    decimal Hours, string DayKind, int RatePercent);

public sealed record LeaveBalanceSummaryDto(
    int Year, decimal EntitledDays, decimal CarriedOverDays, decimal AdjustedDays, decimal UsedDays, decimal PendingDays, decimal RemainingDays);

public sealed record OtSummaryDto(decimal MonthHours, decimal YearHours, decimal MaxPerDay, decimal MaxPerMonth, decimal MaxPerYear);

public sealed record RequestSummaryDto(LeaveBalanceSummaryDto Balance, OtSummaryDto Ot, int PendingApproval, decimal MinNoticeDays);

public sealed record RequestFilter(string Scope, IReadOnlyCollection<string> Statuses, IReadOnlyCollection<Guid> LeaveTypeIds, string? Search, int? Year);

public sealed record CreateLeaveRequest(string? LeaveTypeId, string? FromDate, string? ToDate, string? DurationType, string? Reason, string? AttachmentUrl);

public sealed record CreateOtRequest(string? WorkDate, string? StartTime, string? EndTime, string? ProjectId, string? Reason);

public sealed record DecideRequest(string? Note);

/// <summary>
/// Leave &amp; overtime requests (D4). Approving a request generates read-only time entries
/// (task LEAVE / OT, status approved, source leave_request / overtime_request); rejecting or
/// cancelling it removes them. Parameters come from Admin › Rules (D12).
/// </summary>
public sealed class RequestService(IAppDbContext db, ICurrentUser user, WorkScope scope, RuleService rules, ITenantClock clock)
{
    public const string ScopeMine = "mine";
    public const string ScopeApproval = "approval";
    private const string Pending = "pending";
    private const string Approved = "approved";

    // ─── lists ───────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /leave-requests — scope mine (own requests) or approval (people the user may approve,
    /// without their own unless timesheet.all_edit). Pending first, then newest.
    /// </summary>
    public async Task<PagedResult<LeaveRequestDto>> LeavesAsync(RequestFilter f, PageRequest paging, CancellationToken ct)
    {
        var visible = await VisibleForAsync(f.Scope, ct);
        var query = db.Set<LeaveRequest>().AsNoTracking().Include(r => r.Employee).Include(r => r.LeaveType).Include(r => r.DecidedByNavigation)
            .Where(r => visible.Contains(r.EmployeeId));
        if (f.Statuses.Count > 0) query = query.Where(r => f.Statuses.Contains(r.Status));
        if (f.LeaveTypeIds.Count > 0) query = query.Where(r => f.LeaveTypeIds.Contains(r.LeaveTypeId));
        if (f.Year is { } y) query = query.Where(r => r.FromDate.Year == y);
        var rows = await query.ToListAsync(ct);
        var holidays = await HolidaySetAsync(ct);
        var culture = user.Culture;
        var items = Ordered(rows.Where(r => Text.Matches(f.Search, r.Code, r.Employee.FullName, r.Reason)), r => r.Status, r => r.CreatedAt)
            .Select(r => LeaveDto(r, holidays, culture)).ToList();
        return Paging.Of(items, paging);
    }

    public async Task<PagedResult<OtRequestDto>> OvertimesAsync(RequestFilter f, PageRequest paging, CancellationToken ct)
    {
        var visible = await VisibleForAsync(f.Scope, ct);
        var query = db.Set<OvertimeRequest>().AsNoTracking().Include(r => r.Employee).Include(r => r.Project).Include(r => r.DecidedByNavigation)
            .Where(r => visible.Contains(r.EmployeeId));
        if (f.Statuses.Count > 0) query = query.Where(r => f.Statuses.Contains(r.Status));
        if (f.Year is { } y) query = query.Where(r => r.WorkDate.Year == y);
        var rows = await query.ToListAsync(ct);
        var items = Ordered(rows.Where(r => Text.Matches(f.Search, r.Code, r.Employee.FullName, r.Reason)), r => r.Status, r => r.CreatedAt)
            .Select(OtDto).ToList();
        return Paging.Of(items, paging);
    }

    /// <summary>
    /// GET /leave-requests/summary?year= — own leave fund, own OT hours vs limits, and how many
    /// requests wait for the user's approval.
    /// </summary>
    public async Task<RequestSummaryDto> SummaryAsync(int? yearParam, CancellationToken ct)
    {
        var me = await scope.RequireMyEmployeeIdAsync(ct);
        var employee = await db.Set<Employee>().AsNoTracking().FirstAsync(e => e.Id == me, ct);
        var today = await clock.TodayAsync(ct);
        var set = await rules.SnapshotAsync(ct);
        var ot = await OtTotalsAsync(me, today, null, ct);
        var pending = 0;
        if (user.Has(Permissions.TimesheetApprove))
        {
            var visible = (await scope.VisibleEmployeeIdsAsync(ct)).Where(id => id != me).ToList();
            pending = await db.Set<LeaveRequest>().CountAsync(r => r.Status == Pending && visible.Contains(r.EmployeeId), ct)
                      + await db.Set<OvertimeRequest>().CountAsync(r => r.Status == Pending && visible.Contains(r.EmployeeId), ct);
        }
        return new RequestSummaryDto(
            await BalanceAsync(employee, yearParam ?? today.Year, set, ct),
            new OtSummaryDto(ot.Month, ot.Year, set.Number(RuleKeys.OtMaxHoursPerDay, 4), set.Number(RuleKeys.OtMaxHoursPerMonth, 40), set.Number(RuleKeys.OtMaxHoursPerYear, 200)),
            pending, set.Number(RuleKeys.LeaveMinNoticeDays, 1));
    }

    // ─── create ──────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /leave-requests. Checks: type, dates (to ≥ from; half day = one date), reason, notice of
    /// LEAVE_MIN_NOTICE_DAYS days before the start (not for sick leave), attachment when the type
    /// needs one (metadata.need_attachment), at least one working day, enough balance for types
    /// that deduct it (metadata.deduct_balance), no overlap with another pending / approved request.
    /// total_hours = working days × TIMESHEET_FTE_HOURS (half day = 0.5 day).
    /// </summary>
    public async Task<LeaveRequestDto> CreateLeaveAsync(CreateLeaveRequest r, CancellationToken ct)
    {
        var me = await scope.RequireMyEmployeeIdAsync(ct);
        var employee = await db.Set<Employee>().AsNoTracking().FirstAsync(e => e.Id == me, ct);
        var errors = new ValidationErrors();
        var type = Guid.TryParse(r.LeaveTypeId, out var typeId)
            ? await db.Set<MasterDatum>().AsNoTracking().FirstOrDefaultAsync(m => m.Id == typeId && m.Type == MasterDataTypes.LeaveType && m.IsActive, ct)
            : null;
        if (type is null) errors.Add("leaveTypeId", "validation.required");
        var from = errors.Date("fromDate", r.FromDate);
        var to = errors.Date("toDate", r.ToDate);
        var duration = Text.OrNull(r.DurationType) ?? "full_day";
        if (duration is not ("full_day" or "half_day_am" or "half_day_pm")) errors.Add("durationType", "validation.invalid_value");
        if (from is { } f0 && to is { } t0 && t0 < f0) errors.Add("toDate", "leave_save.err_to_before_from");
        if (from is { } f1 && to is { } t1 && duration != "full_day" && f1 != t1) errors.Add("durationType", "leave_save.err_half_day_range");
        var reason = Text.Clean(r.Reason);
        errors.Required("reason", reason);
        errors.MaxLength("reason", reason, 500);

        var set = await rules.SnapshotAsync(ct);
        var today = await clock.TodayAsync(ct);
        var notice = set.Number(RuleKeys.LeaveMinNoticeDays, 1);
        if (from is { } f2 && type?.Code != "SICK" && set.Enabled(RuleKeys.LeaveMinNoticeDays) && f2 < today.AddDays((int)notice))
            errors.Add("fromDate", "leave_save.err_min_notice", new() { ["days"] = notice });
        var attachment = Text.Clean(r.AttachmentUrl);
        if (type is not null && MetaBool(type.Metadata, "need_attachment") && attachment.Length == 0) errors.Add("attachmentUrl", "leave_save.err_attachment_required");
        var holidays = await HolidaySetAsync(ct);
        decimal days = 0;
        if (from is { } f && to is { } t && t >= f)
        {
            days = LeaveDays(f, t, duration, holidays);
            if (days == 0) errors.Add("toDate", "leave_save.err_no_working_day");
            if (type is not null && MetaBool(type.Metadata, "deduct_balance") && days > (await BalanceAsync(employee, f.Year, set, ct)).RemainingDays)
                errors.Add("leaveTypeId", "leave_save.err_balance");
            if (await db.Set<LeaveRequest>().AnyAsync(x => x.EmployeeId == me && (x.Status == Pending || x.Status == Approved) && x.FromDate <= t && x.ToDate >= f, ct))
                errors.Add("fromDate", "leave_save.err_overlap");
        }
        errors.ThrowIfAny();

        var row = new LeaveRequest
        {
            Code = await NextCodeAsync<LeaveRequest>("NP", ct), EmployeeId = me, LeaveTypeId = type!.Id, FromDate = from!.Value, ToDate = to!.Value,
            DurationType = duration, TotalHours = days * set.Number(RuleKeys.TimesheetFteHours, 8), Reason = reason,
            AttachmentUrl = Text.OrNull(attachment), Status = Pending,
        };
        db.Set<LeaveRequest>().Add(row);
        await db.SaveChangesAsync(ct);
        return await LeaveByIdAsync(row.Id, ct);
    }

    /// <summary>
    /// POST /ot-requests. hours = end − start (e.g. 18:00–21:30 → 3.5 h). Limits OT_MAX_HOURS_PER_DAY /
    /// MONTH / YEAR count approved and pending requests. Rate (D12, Vietnamese labour code by
    /// default): weekday 150 %, weekend 200 %, holiday 300 %.
    /// </summary>
    public async Task<OtRequestDto> CreateOtAsync(CreateOtRequest r, CancellationToken ct)
    {
        var me = await scope.RequireMyEmployeeIdAsync(ct);
        var errors = new ValidationErrors();
        var date = errors.Date("workDate", r.WorkDate);
        var start = ParseTime(r.StartTime);
        var end = ParseTime(r.EndTime);
        if (start is null) errors.Add("startTime", "validation.invalid_time");
        if (end is null) errors.Add("endTime", "validation.invalid_time");
        else if (start is { } s0 && end <= s0) errors.Add("endTime", "ot_save.err_end_before_start");
        Guid? projectId = Guid.TryParse(r.ProjectId, out var pid) ? pid : null;
        if (projectId is { } p && date is { } d0
            && !await db.Set<ProjectAssignment>().AnyAsync(a => a.EmployeeId == me && a.ProjectId == p && a.StartDate <= d0 && a.EndDate >= d0 && !a.Project.IsDeleted, ct))
            errors.Add("projectId", "timesheet_save.err_no_assignment");
        var reason = Text.Clean(r.Reason);
        errors.Required("reason", reason);
        errors.MaxLength("reason", reason, 500);
        var hours = start is { } s && end is { } e && e > s ? WorkDates.Round2((decimal)(e - s).TotalMinutes / 60) : 0;
        var set = await rules.SnapshotAsync(ct);
        if (date is { } d && hours > 0)
        {
            var used = await OtTotalsAsync(me, d, null, ct);
            foreach (var (kind, key, fallback, total) in new[]
                     {
                         ("day", RuleKeys.OtMaxHoursPerDay, 4m, used.Day), ("month", RuleKeys.OtMaxHoursPerMonth, 40m, used.Month),
                         ("year", RuleKeys.OtMaxHoursPerYear, 200m, used.Year),
                     })
            {
                var limit = set.Number(key, fallback);
                if (set.Enabled(key) && total + hours > limit) errors.Add("endTime", $"ot_save.err_limit_{kind}", new() { ["hours"] = limit });
            }
        }
        errors.ThrowIfAny();

        var holidays = await HolidaySetAsync(ct);
        var (dayKind, rate) = holidays.Contains(date!.Value) ? ("holiday", 300) : WorkDates.IsWeekend(date.Value) ? ("weekend", 200) : ("weekday", 150);
        var row = new OvertimeRequest
        {
            Code = await NextCodeAsync<OvertimeRequest>("OT", ct), EmployeeId = me, ProjectId = projectId, WorkDate = date.Value,
            StartTime = start!.Value, EndTime = end!.Value, Hours = hours, DayType = dayKind, RatePercent = (short)rate, Reason = reason, Status = Pending,
        };
        db.Set<OvertimeRequest>().Add(row);
        await db.SaveChangesAsync(ct);
        return await OtByIdAsync(row.Id, ct);
    }

    // ─── decide ──────────────────────────────────────────────────────────────

    /// <summary>
    /// approve / reject (timesheet.approve, a person the user may see, not oneself unless
    /// timesheet.all_edit; a pending request, or reject an approved one; reject needs a reason),
    /// cancel (own pending request, or own approved one not started yet). Returns the request code.
    /// </summary>
    public async Task<(string Code, LeaveRequestDto Dto)> DecideLeaveAsync(Guid id, string action, DecideRequest body, CancellationToken ct)
    {
        var row = await db.Set<LeaveRequest>().FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException();
        await DecideAsync(row.EmployeeId, row.Status, row.FromDate, action, body, ct, (status, note) =>
        {
            row.Status = status;
            if (status != "cancelled") (row.DecidedBy, row.DecidedAt, row.DecisionNote) = (user.UserId, DateTime.UtcNow, note);
        });
        await SyncLeaveLinesAsync(row, ct);
        await db.SaveChangesAsync(ct);
        return (row.Code, await LeaveByIdAsync(row.Id, ct));
    }

    public async Task<(string Code, OtRequestDto Dto)> DecideOtAsync(Guid id, string action, DecideRequest body, CancellationToken ct)
    {
        var row = await db.Set<OvertimeRequest>().FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException();
        await DecideAsync(row.EmployeeId, row.Status, row.WorkDate, action, body, ct, (status, note) =>
        {
            row.Status = status;
            if (status != "cancelled") (row.DecidedBy, row.DecidedAt, row.DecisionNote) = (user.UserId, DateTime.UtcNow, note);
        });
        await SyncOtLinesAsync(row, ct);
        await db.SaveChangesAsync(ct);
        return (row.Code, await OtByIdAsync(row.Id, ct));
    }

    private async Task DecideAsync(Guid employeeId, string status, DateOnly start, string action, DecideRequest body, CancellationToken ct, Action<string, string?> apply)
    {
        var me = await scope.MyEmployeeIdAsync(ct);
        var note = Text.Clean(body.Note);
        if (action == "cancel")
        {
            if (employeeId != me) throw new ForbiddenException();
            if (status is not (Pending or Approved)) throw new ConflictException("request_decide.err_not_cancellable");
            if (status == Approved && start < await clock.TodayAsync(ct)) throw new ConflictException("request_decide.err_past_approved");
            apply("cancelled", null);
            return;
        }
        if (!user.Has(Permissions.TimesheetApprove)) throw new ForbiddenException();
        await scope.EnsureCanViewAsync(employeeId, ct);
        if (employeeId == me && !user.Has(Permissions.TimesheetAllEdit)) throw new ConflictException("request_decide.err_self_approve");
        if (status != Pending && !(action == "reject" && status == Approved)) throw new ConflictException("request_decide.err_not_pending");
        if (action == "reject" && note.Length == 0) throw AppValidationException.Single("note", "request_decide.err_reason_required");
        apply(action == "approve" ? Approved : "rejected", note.Length == 0 ? null : note);
    }

    /// <summary>Approved leave → one approved LEAVE entry per working day (FTE hours, half for half days).</summary>
    private async Task SyncLeaveLinesAsync(LeaveRequest r, CancellationToken ct)
    {
        await RemoveLinesAsync(r.Id, ct);
        if (r.Status != Approved) return;
        var task = await TaskIdAsync("LEAVE", ct);
        var fte = await rules.GetNumberAsync(RuleKeys.TimesheetFteHours, 8, ct);
        var holidays = await HolidaySetAsync(ct);
        foreach (var d in WorkDates.Range(r.FromDate, r.ToDate).Where(d => !WorkDates.IsWeekend(d) && !holidays.Contains(d)))
            db.Set<TimesheetEntry>().Add(new TimesheetEntry
            {
                EmployeeId = r.EmployeeId, TaskTypeId = task, WorkDate = d, Hours = r.DurationType == "full_day" ? fte : fte / 2,
                Description = r.Reason, Status = Approved, Source = TimesheetSources.Leave, SourceRequestId = r.Id,
            });
    }

    /// <summary>Approved OT → one approved OT entry on the project (if any) for the requested hours.</summary>
    private async Task SyncOtLinesAsync(OvertimeRequest r, CancellationToken ct)
    {
        await RemoveLinesAsync(r.Id, ct);
        if (r.Status != Approved) return;
        db.Set<TimesheetEntry>().Add(new TimesheetEntry
        {
            EmployeeId = r.EmployeeId, ProjectId = r.ProjectId, TaskTypeId = await TaskIdAsync("OT", ct), WorkDate = r.WorkDate, Hours = r.Hours,
            Description = r.Reason, Status = Approved, Source = TimesheetSources.Overtime, SourceRequestId = r.Id,
        });
    }

    private async Task RemoveLinesAsync(Guid requestId, CancellationToken ct)
        => db.Set<TimesheetEntry>().RemoveRange(await db.Set<TimesheetEntry>().Where(t => t.SourceRequestId == requestId).ToListAsync(ct));

    private async Task<Guid> TaskIdAsync(string code, CancellationToken ct)
        => await db.Set<MasterDatum>().AsNoTracking().Where(m => m.Type == MasterDataTypes.TaskType && m.Code == code).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct)
           ?? throw new BusinessException("error.conflict");

    // ─── helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Leave fund of a year (D12, parameters in Admin › Rules):
    ///   entitled  = LEAVE_ANNUAL_DAYS + floor(years of service ÷ LEAVE_SENIORITY_STEP_YEARS)
    ///               years of service = year − hire year (≥ 0)
    ///   remaining = entitled + carried over (≤ LEAVE_CARRY_OVER_MAX_DAYS) + adjustment − used
    ///   used / pending = working days of approved / pending ANNUAL leave starting that year
    /// Example: hired 2019, year 2026 → 7 years → 12 + floor(7 / 5) = 13 days.
    /// </summary>
    private async Task<LeaveBalanceSummaryDto> BalanceAsync(Employee e, int year, RuleSet set, CancellationToken ct)
    {
        var row = await db.Set<LeaveBalance>().AsNoTracking().FirstOrDefaultAsync(b => b.EmployeeId == e.Id && b.Year == year, ct);
        var annual = set.Number(RuleKeys.LeaveAnnualDays, 12);
        var step = set.Number(RuleKeys.LeaveSeniorityStepYears, 5);
        var seniority = e.HireDate is { } h ? Math.Max(0, year - h.Year) : 0;
        var entitled = annual + (step > 0 ? Math.Floor(seniority / step) : 0);
        var carried = Math.Min(row?.CarriedOverDays ?? 0, set.Number(RuleKeys.LeaveCarryOverMaxDays, 5));
        var adjusted = row?.AdjustedDays ?? 0;
        var ofYear = await db.Set<LeaveRequest>().AsNoTracking()
            .Where(r => r.EmployeeId == e.Id && r.LeaveType.Code == "ANNUAL" && r.FromDate.Year == year && (r.Status == Approved || r.Status == Pending))
            .ToListAsync(ct);
        var holidays = await HolidaySetAsync(ct);
        var used = ofYear.Where(r => r.Status == Approved).Sum(r => LeaveDays(r.FromDate, r.ToDate, r.DurationType, holidays));
        var pending = ofYear.Where(r => r.Status == Pending).Sum(r => LeaveDays(r.FromDate, r.ToDate, r.DurationType, holidays));
        return new LeaveBalanceSummaryDto(year, entitled, carried, adjusted, used, pending, WorkDates.Round2(entitled + carried + adjusted - used));
    }

    /// <summary>Working days in [from, to] without weekends and holidays; a half day counts 0.5.</summary>
    public static decimal LeaveDays(DateOnly from, DateOnly to, string duration, IReadOnlySet<DateOnly> holidays)
    {
        var days = WorkDates.Range(from, to).Take(400).Count(d => !WorkDates.IsWeekend(d) && !holidays.Contains(d));
        return duration == "full_day" ? days : days * 0.5m;
    }

    /// <summary>OT hours already approved or pending on the day / month / year of <paramref name="day"/>.</summary>
    private async Task<(decimal Day, decimal Month, decimal Year)> OtTotalsAsync(Guid employeeId, DateOnly day, Guid? exclude, CancellationToken ct)
    {
        var rows = await db.Set<OvertimeRequest>().AsNoTracking()
            .Where(r => r.EmployeeId == employeeId && r.Id != exclude && (r.Status == Approved || r.Status == Pending) && r.WorkDate.Year == day.Year)
            .Select(r => new { r.WorkDate, r.Hours }).ToListAsync(ct);
        return (rows.Where(r => r.WorkDate == day).Sum(r => r.Hours), rows.Where(r => r.WorkDate.Month == day.Month).Sum(r => r.Hours), rows.Sum(r => r.Hours));
    }

    private async Task<IReadOnlySet<Guid>> VisibleForAsync(string scopeName, CancellationToken ct)
    {
        var me = await scope.MyEmployeeIdAsync(ct);
        if (scopeName != ScopeApproval) return me is { } m ? new HashSet<Guid> { m } : new HashSet<Guid>();
        if (!user.Has(Permissions.TimesheetApprove)) throw new ForbiddenException();
        var visible = (await scope.VisibleEmployeeIdsAsync(ct)).ToHashSet();
        if (me is { } mine && !user.Has(Permissions.TimesheetAllEdit)) visible.Remove(mine);
        return visible;
    }

    private static IEnumerable<T> Ordered<T>(IEnumerable<T> rows, Func<T, string> status, Func<T, DateTime> created)
        => rows.OrderBy(r => status(r) == Pending ? 0 : 1).ThenByDescending(created);

    private async Task<HashSet<DateOnly>> HolidaySetAsync(CancellationToken ct)
        => (await db.Set<Holiday>().AsNoTracking().Select(h => h.HolidayDate).ToListAsync(ct)).ToHashSet();

    /// <summary>NP-0001, OT-0001… : number of requests of the tenant + 1.</summary>
    private async Task<string> NextCodeAsync<T>(string prefix, CancellationToken ct) where T : class
        => $"{prefix}-{await db.Set<T>().CountAsync(ct) + 1:D4}";

    private async Task<LeaveRequestDto> LeaveByIdAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<LeaveRequest>().AsNoTracking().Include(r => r.Employee).Include(r => r.LeaveType).Include(r => r.DecidedByNavigation)
            .FirstAsync(r => r.Id == id, ct);
        return LeaveDto(row, await HolidaySetAsync(ct), user.Culture);
    }

    private async Task<OtRequestDto> OtByIdAsync(Guid id, CancellationToken ct)
        => OtDto(await db.Set<OvertimeRequest>().AsNoTracking().Include(r => r.Employee).Include(r => r.Project).Include(r => r.DecidedByNavigation)
            .FirstAsync(r => r.Id == id, ct));

    private static LeaveRequestDto LeaveDto(LeaveRequest r, IReadOnlySet<DateOnly> holidays, string culture) => new(
        r.Id, r.Code, WorkScope.Ref(r.Employee), r.Reason, r.Status,
        r.DecidedByNavigation is { } u ? new ApproverRefDto(u.Id, u.FullName) : null, r.DecidedAt, r.DecisionNote, r.CreatedAt,
        MasterDataService.Ref(r.LeaveType, culture), r.FromDate, r.ToDate, r.DurationType,
        LeaveDays(r.FromDate, r.ToDate, r.DurationType, holidays), r.TotalHours, r.AttachmentUrl);

    private static OtRequestDto OtDto(OvertimeRequest r) => new(
        r.Id, r.Code, WorkScope.Ref(r.Employee), r.Reason, r.Status,
        r.DecidedByNavigation is { } u ? new ApproverRefDto(u.Id, u.FullName) : null, r.DecidedAt, r.DecisionNote, r.CreatedAt,
        r.Project is { } p ? new ProjectNameRefDto(p.Id, p.Name) : null, r.WorkDate, WorkDates.Hm(r.StartTime), WorkDates.Hm(r.EndTime),
        r.Hours, r.DayType, r.RatePercent);

    public static TimeOnly? ParseTime(string? s)
        => s is { Length: 5 } && TimeOnly.TryParseExact(s, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var t) ? t : null;

    private static bool MetaBool(string? json, string key)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;
    }
}
