using System.Globalization;
using Dx.Application.Common;
using Dx.Application.Organization;
using Dx.Application.Projects;
using Dx.Application.Settings;
using Dx.Application.Work;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Ai;

/// <summary>
/// AI Insight panel without an LLM (27/10/2026): every notice is a fixed rule over the data the
/// screen already shows, with thresholds from Admin › Rules (module insight + the existing HR /
/// project / attendance rules). Switching a rule off hides the notice it drives. Texts are the API
/// locale keys insight_&lt;screen&gt;.&lt;notice&gt;_title / _content (vi / en).
///
/// Variants (colour of the card): red = act now, amber = watch, purple / teal / blue = information,
/// green = good news / spare capacity.
/// </summary>
public sealed class InsightRuleEngine(
    IAppDbContext db, ICurrentUser user, IMessageLocalizer text, RuleService rules, ITenantClock clock,
    ProjectService projects, ProjectDetailService projectDetails, ProjectTimelineService timeline,
    EmployeeService employees, WorkScope scope, RequestService requests, AttendanceService attendance, EffortService effort)
{
    private const string ProjectTaskCode = "PROJECT";

    /// <summary>Notices of one screen (not its parents — the caller walks the ADR-0001 chain).</summary>
    public async Task<IReadOnlyList<InsightItemDto>> ForScreenAsync(string screen, Guid? projectId, CancellationToken ct)
    {
        var set = await rules.SnapshotAsync(ct);
        var notices = new Notices(screen, text, user.Culture, (int)set.Number("INSIGHT_MAX_NAMES", 3));
        switch (screen)
        {
            case "project_list": await ProjectListAsync(notices, set, ct); break;
            case "project_detail" when projectId is { } p: await ProjectDetailAsync(notices, set, p, ct); break;
            case "project_detail_metrics_timeline" when projectId is { } p: await MilestonesAsync(notices, set, p, ct); break;
            case "employee_list" when user.Has(Permissions.EmployeeView): await EmployeeListAsync(notices, set, ct); break;
            case "timesheet_week": await TimesheetWeekAsync(notices, set, ct); break;
            case "timesheet_day": await TimesheetDayAsync(notices, ct); break;
            case "leave_request": await LeaveAsync(notices, set, ct); break;
            case "attendance": await AttendanceAsync(notices, ct); break;
            case "effort" when user.Has(Permissions.EffortView): await EffortAsync(notices, set, ct); break;
        }
        return notices.Items;
    }

    // ─── projects ────────────────────────────────────────────────────────────

    /// <summary>
    /// Project list (projects the user may see, not in the trash):
    ///   high_risk   red    risk level HIGH
    ///   over_budget red    budget used ≥ PROJECT_BUDGET_WARNING_THRESHOLD %
    ///   ending_soon amber  end date within PROJECT_ENDING_SOON_DAYS days (or status ENDING), not passed
    ///   planning    purple status PLANNING (contract staffing still to be settled)
    ///   all_good    green  none of the above
    /// </summary>
    private async Task ProjectListAsync(Notices n, RuleSet set, CancellationToken ct)
    {
        if (!user.Has(Permissions.ProjectView)) return;
        var rows = (await projects.SearchAsync(new ProjectFilter(null, [], [], [], [], null, null, false), new PageRequest(1, 0), ct)).Items;
        var today = await clock.TodayAsync(ct);
        var endingDays = (int)set.Number(RuleKeys.ProjectEndingSoonDays, 30);
        var budgetMax = set.Number(RuleKeys.ProjectBudgetWarningThreshold, 75);

        var risky = rows.Where(p => p.RiskLevel?.Code == MasterDataCodes.RiskLevelHigh).ToList();
        n.Add("high_risk", "red", risky.Count, risky.Select(p => p.Name));
        if (set.Enabled(RuleKeys.ProjectBudgetWarningThreshold))
        {
            var over = rows.Where(p => p.BudgetUsedPercent >= budgetMax).ToList();
            n.Add("over_budget", "red", over.Count, over.Select(p => p.Name), ("threshold", budgetMax));
        }
        if (set.Enabled(RuleKeys.ProjectEndingSoonDays))
        {
            var ending = rows.Where(p => p.EndDate is { } e && e >= today
                                         && (p.Status?.Code == MasterDataCodes.ProjectStatusEnding || e.DayNumber - today.DayNumber <= endingDays)).ToList();
            n.Add("ending_soon", "amber", ending.Count, ending.Select(p => p.Name), ("days", endingDays));
        }
        var planning = rows.Where(p => p.Status?.Code == MasterDataCodes.ProjectStatusPlanning).ToList();
        n.Add("planning", "purple", planning.Count, planning.Select(p => p.Name));
        n.AddIfEmpty("all_good", "green");
    }

    /// <summary>
    /// One project (Overview tab and its children):
    ///   missing_positions amber  contract positions running today − positions filled today
    ///   fte_gap           amber  contract FTE − assigned FTE ≥ INSIGHT_FTE_GAP_MIN (managers only)
    ///   over_budget       red    budget used ≥ PROJECT_BUDGET_WARNING_THRESHOLD %
    ///   hr_alert          amber  assigned people who left / are leaving without a replacement
    ///   all_good          green  none of the above
    /// </summary>
    private async Task ProjectDetailAsync(Notices n, RuleSet set, Guid projectId, CancellationToken ct)
    {
        var p = await projectDetails.GetAsync(projectId, ct); // 403 / 404 like the detail screen
        var today = await clock.TodayAsync(ct);
        if (p.CanManage && !p.IsDeleted)
        {
            var contract = await db.Set<ProjectContractStaff>().AsNoTracking()
                .Where(s => s.ProjectId == projectId && s.StartDate <= today && s.EndDate >= today).SumAsync(s => (int)s.Quantity, ct);
            var filled = await db.Set<ProjectAssignment>().AsNoTracking()
                .Where(a => a.ProjectId == projectId && a.ContractStaffId != null && a.StartDate <= today && a.EndDate >= today)
                .Select(a => new { a.ContractStaffId, a.SlotIndex }).Distinct().CountAsync(ct);
            n.Add("missing_positions", "amber", Math.Max(0, contract - filled), [], ("contract", contract), ("assigned", filled));

            var gapMin = set.Number("INSIGHT_FTE_GAP_MIN", 0.5m);
            var gap = Math.Round(p.ContractStaffFte - p.ActualStaffFte, 2);
            if (set.Enabled("INSIGHT_FTE_GAP_MIN") && p.ContractStaffFte > 0 && gap >= gapMin)
                n.Add("fte_gap", "amber", 1, [], ("gap", gap), ("contract", p.ContractStaffFte), ("actual", p.ActualStaffFte));
        }
        var budgetMax = set.Number(RuleKeys.ProjectBudgetWarningThreshold, 75);
        if (set.Enabled(RuleKeys.ProjectBudgetWarningThreshold) && p.BudgetUsedPercent >= budgetMax)
            n.Add("over_budget", "red", 1, [], ("percent", p.BudgetUsedPercent), ("threshold", budgetMax));
        n.Add("hr_alert", "amber", p.HrAlerts.Count, p.HrAlerts.Select(a => a.FullName));
        n.AddIfEmpty("all_good", "green");
    }

    /// <summary>
    /// Timeline tab:
    ///   overdue  red    not done, planned date passed (how many still lack a justification)
    ///   upcoming amber  not done, planned within INSIGHT_MILESTONE_UPCOMING_DAYS days
    /// </summary>
    private async Task MilestonesAsync(Notices n, RuleSet set, Guid projectId, CancellationToken ct)
    {
        var m = await timeline.MilestonesAsync(projectId, null, null, ct);
        var overdue = m.Items.Where(i => i.Status == "overdue").ToList();
        n.Add("overdue", "red", overdue.Count, overdue.Select(i => i.Name), ("unjustified", overdue.Count(i => string.IsNullOrEmpty(i.Justification))));
        if (set.Enabled("INSIGHT_MILESTONE_UPCOMING_DAYS"))
        {
            var days = (int)set.Number("INSIGHT_MILESTONE_UPCOMING_DAYS", 7);
            var upcoming = m.Items.Where(i => i.ActualDate is null && i.PlannedDate >= m.Today && i.PlannedDate.DayNumber - m.Today.DayNumber <= days).ToList();
            n.Add("upcoming", "amber", upcoming.Count, upcoming.Select(i => i.Name), ("days", days));
        }
    }

    // ─── people ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Employees (working people, allocation of assignments running today):
    ///   overloaded  amber allocation &gt; HR_OVERLOAD_ALLOCATION_THRESHOLD %
    ///   bench       green allocation &lt; HR_MIN_ALLOCATION_THRESHOLD % (spare capacity)
    ///   utilization blue  utilization below HR_UTILIZATION_TARGET_PERCENT
    /// </summary>
    private async Task EmployeeListAsync(Notices n, RuleSet set, CancellationToken ct)
    {
        var all = new EmployeeFilter(null, [], [], [], [], [], [], false);
        var rows = (await employees.SearchAsync(all, new PageRequest(1, 0), ct)).Items;
        var summary = await employees.SummaryAsync(all, ct);
        if (set.Enabled(RuleKeys.HrOverloadAllocationThreshold))
        {
            var over = rows.Where(e => e.AllocationStatus == EmployeeService.AllocationOverloaded).OrderByDescending(e => e.AllocationPercent).ToList();
            n.Add("overloaded", "amber", over.Count, over.Select(e => e.FullName), ("threshold", set.Number(RuleKeys.HrOverloadAllocationThreshold, 100)));
        }
        if (set.Enabled(RuleKeys.HrMinAllocationThreshold))
        {
            var free = rows.Where(e => e.AllocationStatus is EmployeeService.AllocationBench or EmployeeService.AllocationLow).ToList();
            n.Add("bench", "green", free.Count, free.Select(e => e.FullName), ("threshold", set.Number(RuleKeys.HrMinAllocationThreshold, 20)));
        }
        if (set.Enabled(RuleKeys.HrUtilizationTargetPercent) && summary.Total > 0 && summary.UtilizationPercent < summary.UtilizationTarget)
            n.Add("utilization", "blue", 1, [], ("percent", summary.UtilizationPercent), ("target", summary.UtilizationTarget));
    }

    /// <summary>
    /// Own work log of this week (Monday → today):
    ///   week_hours  teal   logged vs expected = working days so far (no weekends / holidays) × TIMESHEET_FTE_HOURS,
    ///                      e.g. Thursday: 4 × 8 = 32 h expected; days without any entry are listed
    ///   week_share  purple share of project time vs other tasks this week
    /// </summary>
    private async Task TimesheetWeekAsync(Notices n, RuleSet set, CancellationToken ct)
    {
        if (await scope.MyEmployeeIdAsync(ct) is not { } me) return;
        var today = await clock.TodayAsync(ct);
        var start = WorkDates.WeekStart(today);
        var entries = await db.Set<TimesheetEntry>().AsNoTracking().Include(t => t.TaskType)
            .Where(t => t.EmployeeId == me && t.WorkDate >= start && t.WorkDate <= today).ToListAsync(ct);
        var holidays = await db.Set<Holiday>().AsNoTracking().Where(h => h.HolidayDate >= start && h.HolidayDate <= today).Select(h => h.HolidayDate).ToListAsync(ct);
        var workdays = WorkDates.Range(start, today).Where(d => !WorkDates.IsWeekend(d) && !holidays.Contains(d)).ToList();
        var fte = set.Number(RuleKeys.TimesheetFteHours, 8);
        var expected = workdays.Count * fte;
        var logged = entries.Sum(t => t.Hours);
        var missingDays = workdays.Where(d => entries.All(t => t.WorkDate != d)).ToList();
        var dayNames = missingDays.Select(d => d.ToString(user.Culture == "en" ? "ddd dd/MM" : "dd/MM", CultureInfo.GetCultureInfo(user.Culture == "en" ? "en-US" : "vi-VN")));
        if (expected > 0)
            n.Add(logged >= expected ? "week_complete" : "week_hours", logged >= expected ? "green" : "teal", 1, dayNames,
                ("logged", WorkDates.Round2(logged)), ("expected", expected), ("missing", WorkDates.Round2(Math.Max(0, expected - logged))), ("days", missingDays.Count));
        if (logged > 0)
        {
            var project = entries.Where(t => t.ProjectId != null || t.TaskType.Code == ProjectTaskCode).Sum(t => t.Hours);
            var projectPct = (int)Math.Round(project / logged * 100, MidpointRounding.AwayFromZero);
            n.Add("week_share", "purple", 1, [], ("project", projectPct), ("other", 100 - projectPct));
        }
    }

    /// <summary>Own work log, previous working day: what was logged (to copy it), or a reminder when nothing was.</summary>
    private async Task TimesheetDayAsync(Notices n, CancellationToken ct)
    {
        if (await scope.MyEmployeeIdAsync(ct) is not { } me) return;
        var day = (await clock.TodayAsync(ct)).AddDays(-1);
        while (WorkDates.IsWeekend(day)) day = day.AddDays(-1);
        var entries = await db.Set<TimesheetEntry>().AsNoTracking().Include(t => t.Project).Include(t => t.TaskType)
            .Where(t => t.EmployeeId == me && t.WorkDate == day).ToListAsync(ct);
        if (entries.Count == 0)
        {
            n.Add("yesterday_empty", "amber", 1, [], ("date", day.ToString("dd/MM", CultureInfo.InvariantCulture)));
            return;
        }
        var lines = entries.GroupBy(t => t.Project?.Name ?? Localized.Name(user.Culture, t.TaskType.Name, t.TaskType.NameEn))
            .Select(g => $"{WorkDates.Round2(g.Sum(t => t.Hours)).ToString(CultureInfo.InvariantCulture)}h {g.Key}");
        n.Add("yesterday", "blue", 1, lines, ("hours", WorkDates.Round2(entries.Sum(t => t.Hours))));
    }

    /// <summary>
    /// Leave &amp; OT:
    ///   pending  amber requests waiting for the user's approval
    ///   balance  blue  from month INSIGHT_LEAVE_REMINDER_MONTH, own annual leave left ≥ INSIGHT_LEAVE_REMAINING_DAYS
    /// </summary>
    private async Task LeaveAsync(Notices n, RuleSet set, CancellationToken ct)
    {
        if (await scope.MyEmployeeIdAsync(ct) is null) return;
        var summary = await requests.SummaryAsync(null, ct);
        if (user.Has(Permissions.TimesheetApprove) && summary.PendingApproval > 0)
            n.Add("pending", "amber", summary.PendingApproval, []);
        var today = await clock.TodayAsync(ct);
        if (set.Enabled("INSIGHT_LEAVE_REMAINING_DAYS") && today.Month >= set.Number("INSIGHT_LEAVE_REMINDER_MONTH", 10)
            && summary.Balance.RemainingDays >= set.Number("INSIGHT_LEAVE_REMAINING_DAYS", 8))
            n.Add("balance", "blue", 1, [], ("days", summary.Balance.RemainingDays));
    }

    /// <summary>
    /// Check-in / out of this month (everyone with timesheet.all_view, otherwise oneself):
    ///   late        red   check-ins after work_start_time + ATTENDANCE_LATE_GRACE_MINUTES
    ///   missing_out amber past days with a check-in but no check-out
    /// </summary>
    private async Task AttendanceAsync(Notices n, CancellationToken ct)
    {
        if (!user.Has(Permissions.TimesheetEdit) && !user.Has(Permissions.TimesheetAllView) && !user.Has(Permissions.TimesheetAllEdit)) return;
        var month = await attendance.MonthAsync(new AttendanceFilter(null, null, [], false), new PageRequest(1, 0), ct);
        var late = month.Items.Where(r => r.Summary.LateCount > 0).OrderByDescending(r => r.Summary.LateCount).ToList();
        n.Add("late", "red", late.Sum(r => r.Summary.LateCount), late.Select(r => r.Employee.FullName), ("start", month.WorkStartTime), ("grace", month.GraceMinutes));
        var missing = month.Items.Where(r => r.Summary.MissingOutCount > 0).ToList();
        n.Add("missing_out", "amber", missing.Sum(r => r.Summary.MissingOutCount), missing.Select(r => r.Employee.FullName));
    }

    /// <summary>
    /// Effort grid of this month, per person (same formulas as the grid, effort-calculations.ts):
    ///   Actual of a person = Σ (actual effort + OT effort) over their rows
    ///   overload red   Actual &gt; INSIGHT_EFFORT_OVER_MM
    ///   plan_gap amber some row of the person has |Thực tế − Kế hoạch| = |TtEffort − Hd| &gt; INSIGHT_EFFORT_PLAN_GAP_MM
    ///                  (per row = person × project × role, OT not counted; above or below the plan both count),
    ///                  e.g. Kế hoạch 1, Thực tế 0.7 → gap 0.3 &gt; 0.2 → listed; Kế hoạch 0.5, Thực tế 0.65 → 0.15 → not listed
    ///   free     green Free = 1 − Actual &gt; 0 → total spare = Σ Free in MM
    /// </summary>
    private async Task EffortAsync(Notices n, RuleSet set, CancellationToken ct)
    {
        var grid = await effort.GridAsync(null, ct);
        var people = grid.Rows.Where(r => r.Employee is not null).GroupBy(r => r.Employee!.Id)
            .Select(g => new { g.First().Employee!.FullName, Actual = g.Sum(r => r.TtEffort + r.OtEffort) }).ToList();
        if (set.Enabled("INSIGHT_EFFORT_OVER_MM"))
        {
            var limit = set.Number("INSIGHT_EFFORT_OVER_MM", 1);
            var over = people.Where(p => p.Actual > limit).ToList();
            n.Add("overload", "red", over.Count, over.Select(p => p.FullName), ("mm", limit), ("month", grid.Month));
        }
        if (set.Enabled("INSIGHT_EFFORT_PLAN_GAP_MM"))
        {
            var gapMax = set.Number("INSIGHT_EFFORT_PLAN_GAP_MM", 0.2m);
            var offRows = grid.Rows.Where(r => r.Employee is not null && Math.Abs(r.TtEffort - r.Hd) > gapMax).ToList();
            var offPeople = offRows.GroupBy(r => r.Employee!.Id).Select(g => g.First().Employee!.FullName).ToList();
            n.Add("plan_gap", "amber", offPeople.Count, offPeople, ("gap", gapMax), ("rows", offRows.Count), ("month", grid.Month));
        }
        var free = people.Where(p => 1 - p.Actual > 0.001m).ToList();
        n.Add("free", "green", free.Count, free.Select(p => p.FullName), ("total", WorkDates.Round2(free.Sum(p => 1 - p.Actual))));
    }

    /// <summary>Collects the notices of one screen; a notice with count 0 is skipped.</summary>
    private sealed class Notices(string screen, IMessageLocalizer text, string culture, int maxNames)
    {
        public List<InsightItemDto> Items { get; } = [];

        public void Add(string code, string variant, int count, IEnumerable<string> names, params (string Key, object Value)[] extra)
        {
            if (count <= 0) return;
            var list = names.ToList();
            var shown = string.Join(", ", list.Take(Math.Max(1, maxNames)));
            if (list.Count > maxNames) shown += culture == "en" ? $" and {list.Count - maxNames} more" : $" và {list.Count - maxNames} mục khác";
            var args = new Dictionary<string, object?> { ["count"] = count, ["names"] = shown };
            foreach (var (k, v) in extra) args[k] = v is decimal d ? d.ToString("0.##", CultureInfo.InvariantCulture) : v;
            Items.Add(new InsightItemDto($"{screen}-{code}", screen, variant,
                text.Text($"insight_{screen}.{code}_title", args, culture), text.Text($"insight_{screen}.{code}_content", args, culture)));
        }

        public void AddIfEmpty(string code, string variant)
        {
            if (Items.Count == 0) Add(code, variant, 1, []);
        }
    }
}
