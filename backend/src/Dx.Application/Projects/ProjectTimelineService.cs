using System.Text.Json;
using Dx.Application.Common;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Projects;

public sealed record MilestoneItemDto(
    Guid Id, string Name, DateOnly PlannedDate, DateOnly? ActualDate, int SortSeq, string Status, int DelayDays,
    string? Justification, string? JustificationBy, DateTime? JustificationAt);

public sealed record TimelinessDto(DateOnly From, DateOnly To, int DueCount, int OnTimeCount, int? Percent);

public sealed record MilestonesDto(
    DateOnly StartDate, DateOnly EndDate, DateOnly Today, IReadOnlyList<MilestoneItemDto> Items, TimelinessDto Timeliness, bool CanEdit);

public sealed record SaveMilestoneRequest(string? Name, string? PlannedDate, string? ActualDate, string? Justification);

public sealed record ProjectDatesRequest(string? StartDate, string? EndDate);

public sealed record EeMonthDto(
    string Month, decimal HoursPerMM, decimal HdMM, decimal ActualMM, decimal HdHours, decimal ActualHours, decimal LogworkHours,
    decimal? EeMonth, string EeMonthTone, decimal HdYtdHours, decimal ActualYtdHours, decimal LogworkYtdHours, decimal? EeYtd, string EeYtdTone);

public sealed record EePointDto(DateOnly Date, decimal Hd, decimal Actual, decimal Logwork);

public sealed record EeTotalsDto(decimal HdHours, decimal ActualHours, decimal LogworkHours, decimal? Ee, string EeTone);

public sealed record EeMemberDto(Guid EmployeeId, string FullName, string Code, string Position, IReadOnlyList<EePointDto> Points, EeTotalsDto Totals);

public sealed record EeThresholdsDto(decimal GreenMax, decimal OrangeMax);

public sealed record EeDto(IReadOnlyList<EeMonthDto> Months, string? ChartMonth, IReadOnlyList<EeMemberDto> Members, EeThresholdsDto Thresholds);

public sealed record QualityRowDto(
    string Month, bool HasData, int Defects, decimal ProductSize, string ProductSizeUnit, decimal BillableMM,
    decimal? RatePerSize, decimal? RatePerMM, string RatePerMMTone, int DefectsYtd, decimal ProductSizeYtd, decimal BillableYtd,
    decimal? RatePerSizeYtd, decimal? RatePerMMYtd, string RatePerMMYtdTone);

public sealed record RateThresholdsDto(decimal GoodMin, decimal GoodMax, decimal WarnMax);

public sealed record QualityDto(
    string Kind, int Year, IReadOnlyList<int> Years, string From, string To, string Unit, IReadOnlyList<QualityRowDto> Rows,
    RateThresholdsDto Thresholds, bool CanEdit);

public sealed record QualityEntryRequest(string? Month, JsonElement? Defects, JsonElement? ProductSize);

public sealed record SaveQualityRequest(string? Kind, string? Unit, JsonElement? Year, IReadOnlyList<QualityEntryRequest>? Entries);

/// <summary>Project timeline (milestones, project dates) and the Metrics tab figures EE and defect / leakage rates.</summary>
public sealed class ProjectTimelineService(IAppDbContext db, ICurrentUser user, ProjectAccess access, Settings.RuleService rules, ITenantClock clock)
{
    private static readonly string[] ProductSizeUnits = ["Point", "Request", "Module", "KLOC"];

    /// <summary>Hours in one man-day when converting MM to hours (EE).</summary>
    private const int StandardHours = 8;

    // ─── milestones ──────────────────────────────────────────────────────────

    /// <summary>
    /// GET /projects/{id}/milestones?from=&amp;to=. Status of a milestone:
    ///   done → done-ontime when actual ≤ planned, else done-late
    ///   not done and planned &lt; today → overdue
    ///   future ones: the nearest is next, the others upcoming
    /// delayDays = actual − planned (≥ 0) when done, today − planned when overdue, else 0.
    /// Timeliness over [from, to] (default project range) = on-time ÷ due × 100 (rounded), "due"
    /// meaning planned inside the range and not next / upcoming; nothing due → null.
    /// Example: 4 due, 3 on time → 75 %.
    /// </summary>
    public async Task<MilestonesDto> MilestonesAsync(Guid projectId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var scope = await access.OpenAsync(projectId, ct);
        var p = scope.Project;
        var today = await clock.TodayAsync(ct);
        var rows = await db.Set<ProjectMilestone>().AsNoTracking().Include(m => m.JustificationByNavigation)
            .Where(m => m.ProjectId == p.Id).OrderBy(m => m.PlannedDate).ThenBy(m => m.SortOrder).ToListAsync(ct);

        var statuses = new Dictionary<Guid, string>();
        var future = new List<ProjectMilestone>();
        foreach (var m in rows)
        {
            if (m.ActualDate is { } actual) statuses[m.Id] = actual <= m.PlannedDate ? "done-ontime" : "done-late";
            else if (m.PlannedDate < today) statuses[m.Id] = "overdue";
            else future.Add(m);
        }
        foreach (var (m, i) in future.OrderBy(m => m.PlannedDate).Select((m, i) => (m, i))) statuses[m.Id] = i == 0 ? "next" : "upcoming";

        var start = p.StartDate ?? rows.FirstOrDefault()?.PlannedDate ?? today;
        var end = p.EndDate ?? rows.LastOrDefault()?.PlannedDate ?? today;
        var rangeFrom = from ?? start;
        var rangeTo = to ?? end;
        var due = rows.Where(m => m.PlannedDate >= rangeFrom && m.PlannedDate <= rangeTo && statuses[m.Id] is not ("next" or "upcoming")).ToList();
        var onTime = due.Count(m => statuses[m.Id] == "done-ontime");
        var timeliness = new TimelinessDto(rangeFrom, rangeTo, due.Count, onTime,
            due.Count == 0 ? null : (int)Math.Round((decimal)onTime / due.Count * 100, MidpointRounding.AwayFromZero));

        return new MilestonesDto(start, end, today, rows.Select(m => new MilestoneItemDto(
            m.Id, m.Name, m.PlannedDate, m.ActualDate, m.SortOrder, statuses[m.Id],
            m.ActualDate is { } a ? Math.Max(0, a.DayNumber - m.PlannedDate.DayNumber) : m.PlannedDate < today ? today.DayNumber - m.PlannedDate.DayNumber : 0,
            m.Justification, m.JustificationByNavigation?.FullName, m.JustificationAt)).ToList(),
            timeliness, scope.CanManage && !p.IsDeleted);
    }

    /// <summary>PATCH /projects/{id}/dates — quick edit from the project timeline.</summary>
    public async Task UpdateDatesAsync(Guid projectId, ProjectDatesRequest r, CancellationToken ct)
    {
        var scope = await access.ManageAsync(projectId, ct, tracking: true);
        var errors = new ValidationErrors();
        var start = errors.Date("startDate", r.StartDate);
        var end = errors.Date("endDate", r.EndDate);
        if (start is { } s && end is { } e && e < s) errors.Add("endDate", "project_save.err_end_before_start");
        errors.ThrowIfAny();
        scope.Project.StartDate = start;
        scope.Project.EndDate = end;
        await db.SaveChangesAsync(ct);
    }

    public async Task<string> CreateMilestoneAsync(Guid projectId, SaveMilestoneRequest r, CancellationToken ct)
    {
        var scope = await access.ManageAsync(projectId, ct);
        var input = await ParseMilestoneAsync(r, ct);
        var next = (await db.Set<ProjectMilestone>().IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(m => m.ProjectId == scope.Project.Id).MaxAsync(m => (int?)m.SortOrder, ct) ?? 0) + 1;
        var row = new ProjectMilestone { ProjectId = scope.Project.Id, Name = input.Name, SortOrder = next, PlannedDate = input.Planned, ActualDate = input.Actual };
        SetJustification(row, input.Justification);
        db.Set<ProjectMilestone>().Add(row);
        await db.SaveChangesAsync(ct);
        return row.Name;
    }

    /// <summary>The author / time of the justification change only when its text changes.</summary>
    public async Task<string> UpdateMilestoneAsync(Guid projectId, Guid milestoneId, SaveMilestoneRequest r, CancellationToken ct)
    {
        var scope = await access.ManageAsync(projectId, ct);
        var row = await db.Set<ProjectMilestone>().FirstOrDefaultAsync(m => m.Id == milestoneId && m.ProjectId == scope.Project.Id, ct) ?? throw new NotFoundException();
        var input = await ParseMilestoneAsync(r, ct);
        row.Name = input.Name;
        row.PlannedDate = input.Planned;
        row.ActualDate = input.Actual;
        if (input.Justification != row.Justification) SetJustification(row, input.Justification);
        await db.SaveChangesAsync(ct);
        return row.Name;
    }

    public async Task<string> DeleteMilestoneAsync(Guid projectId, Guid milestoneId, CancellationToken ct)
    {
        var scope = await access.ManageAsync(projectId, ct);
        var row = await db.Set<ProjectMilestone>().FirstOrDefaultAsync(m => m.Id == milestoneId && m.ProjectId == scope.Project.Id, ct) ?? throw new NotFoundException();
        row.IsDeleted = true;
        row.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return row.Name;
    }

    private void SetJustification(ProjectMilestone row, string? text)
    {
        row.Justification = text;
        row.JustificationBy = text is null ? null : user.UserId;
        row.JustificationAt = text is null ? null : DateTime.UtcNow;
    }

    private async Task<(string Name, DateOnly Planned, DateOnly? Actual, string? Justification)> ParseMilestoneAsync(SaveMilestoneRequest r, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var name = Text.Clean(r.Name);
        errors.Required("name", name);
        errors.MaxLength("name", name, 200);
        var planned = errors.Date("plannedDate", r.PlannedDate);
        var actual = errors.Date("actualDate", r.ActualDate, optional: true);
        if (actual is { } a && a > await clock.TodayAsync(ct)) errors.Add("actualDate", "project_milestone.err_actual_future");
        var justification = Text.Clean(r.Justification);
        errors.MaxLength("justification", justification, 1000);
        errors.ThrowIfAny();
        return (name, planned!.Value, actual, Text.OrNull(justification));
    }

    // ─── EE ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /projects/{id}/ee?month=yyyy-MM — Effort Efficiency per month (from the Effort grid):
    ///   contract MM of a person = effort_allocations.contract_effort; actual MM = actual_effort × allocation_rate / 100
    ///   1 MM in hours = Mon–Fri days of the month × 8 (holidays not removed), so each month differs
    ///   EE month (%) = actual hours ÷ contract hours × 100; contract 0 → null
    ///   EE year-to-date = Σ actual ÷ Σ contract of the months with an EE in the same calendar year so far
    ///   logwork = hours logged on the project (shown for comparison, not used in EE)
    /// Example: October 2026 has 22 weekdays → 1 MM = 176 h; contract 2 MM = 352 h, actual 1.8 MM =
    /// 316.8 h → EE = 90 %. Member chart of the month: cumulative per day, contract / actual spread
    /// over weekdays (MM × 8 h a day). A user without project.manage only sees their own rows.
    /// </summary>
    public async Task<EeDto> EeAsync(Guid projectId, string? monthParam, CancellationToken ct)
    {
        var scope = await access.OpenAsync(projectId, ct);
        var p = scope.Project;
        Guid? onlyMine = scope.CanManage ? null : scope.EmployeeId ?? Guid.Empty;

        var allocations = await db.Set<EffortAllocation>().AsNoTracking()
            .Where(a => a.ProjectId == p.Id && (onlyMine == null || a.EmployeeId == onlyMine))
            .Select(a => new { a.EmployeeId, a.PeriodMonth, a.ContractEffort, a.ActualEffort, a.AllocationRate }).ToListAsync(ct);
        var logs = await db.Set<TimesheetEntry>().AsNoTracking()
            .Where(t => t.ProjectId == p.Id && (onlyMine == null || t.EmployeeId == onlyMine))
            .GroupBy(t => new { t.EmployeeId, t.WorkDate }).Select(g => new { g.Key.EmployeeId, g.Key.WorkDate, Hours = g.Sum(t => t.Hours) })
            .ToListAsync(ct);
        var people = allocations.Select(a => a.EmployeeId).Concat(logs.Select(l => l.EmployeeId)).Distinct().ToList();
        var culture = user.Culture;
        var employees = await db.Set<Employee>().IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking().Include(e => e.Position)
            .Where(e => people.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);

        var rows = new Dictionary<(Guid, string), EeRow>();
        EeRow RowFor(Guid employeeId, string month)
        {
            if (!rows.TryGetValue((employeeId, month), out var r))
            {
                var e = employees.GetValueOrDefault(employeeId);
                r = new EeRow(month, employeeId, e?.FullName ?? string.Empty, e?.Code ?? string.Empty,
                    e?.Position is { } pos ? Localized.Name(culture, pos.Name, pos.NameEn) : string.Empty);
                rows[(employeeId, month)] = r;
            }
            return r;
        }
        foreach (var a in allocations)
        {
            var r = RowFor(a.EmployeeId, ProjectDates.MonthKey(a.PeriodMonth));
            r.HdMM += a.ContractEffort;
            r.ActualMM += a.ActualEffort * a.AllocationRate / 100m;
        }
        foreach (var l in logs)
        {
            var r = RowFor(l.EmployeeId, ProjectDates.MonthKey(l.WorkDate));
            r.LogworkHours += l.Hours;
            r.LogDaily[l.WorkDate] = r.LogDaily.GetValueOrDefault(l.WorkDate) + l.Hours;
        }

        var t = await QualityThresholds.LoadAsync(rules, ct);
        static decimal? Ee(decimal actual, decimal hd) => hd > 0 ? ProjectDates.Round2(actual / hd * 100) : null;
        var entries = rows.Values.ToList();
        var months = entries.Select(e => e.Month).Distinct().Order(StringComparer.Ordinal).ToList();
        var ytdByYear = new Dictionary<string, (decimal Hd, decimal Actual, decimal Logwork)>();
        var monthly = months.Select(month =>
        {
            var list = entries.Where(e => e.Month == month).ToList();
            var perMM = ProjectDates.WorkingDaysOfMonth(MonthStart(month)) * StandardHours;
            var hdMM = list.Sum(e => e.HdMM);
            var actualMM = list.Sum(e => e.ActualMM);
            var logwork = list.Sum(e => e.LogworkHours);
            var hdHours = hdMM * perMM;
            var actualHours = actualMM * perMM;
            var eeMonth = Ee(actualHours, hdHours);
            var year = month[..4];
            var ytd = ytdByYear.GetValueOrDefault(year);
            if (eeMonth is not null) ytd = (ytd.Hd + hdHours, ytd.Actual + actualHours, ytd.Logwork + logwork);
            ytdByYear[year] = ytd;
            var eeYtd = Ee(ytd.Actual, ytd.Hd);
            return new EeMonthDto(month, perMM, ProjectDates.Round2(hdMM), ProjectDates.Round2(actualMM), ProjectDates.Round2(hdHours),
                ProjectDates.Round2(actualHours), ProjectDates.Round2(logwork), eeMonth, t.EeTone(eeMonth),
                ProjectDates.Round2(ytd.Hd), ProjectDates.Round2(ytd.Actual), ProjectDates.Round2(ytd.Logwork), eeYtd, t.EeTone(eeYtd));
        }).ToList();

        var chartMonth = monthParam is not null && months.Contains(monthParam) ? monthParam : months.LastOrDefault();
        var vi = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: false);
        var members = chartMonth is null ? [] : entries.Where(e => e.Month == chartMonth).OrderBy(e => e.FullName, vi).Select(e =>
        {
            decimal hd = 0, actual = 0, logwork = 0;
            var points = ProjectDates.DaysOfMonth(MonthStart(chartMonth)).Select(d =>
            {
                var working = !ProjectDates.IsWeekend(d);
                hd += working ? e.HdMM * StandardHours : 0;
                actual += working ? e.ActualMM * StandardHours : 0;
                logwork += e.LogDaily.GetValueOrDefault(d);
                return new EePointDto(d, ProjectDates.Round2(hd), ProjectDates.Round2(actual), ProjectDates.Round2(logwork));
            }).ToList();
            var value = Ee(actual, hd);
            return new EeMemberDto(e.EmployeeId, e.FullName, e.Code, e.Position, points,
                new EeTotalsDto(ProjectDates.Round2(hd), ProjectDates.Round2(actual), ProjectDates.Round2(logwork), value, t.EeTone(value)));
        }).ToList();
        return new EeDto(monthly, chartMonth, members, new EeThresholdsDto(t.EeGreenMax, t.EeOrangeMax));
    }

    private sealed class EeRow(string month, Guid employeeId, string fullName, string code, string position)
    {
        public string Month { get; } = month;
        public Guid EmployeeId { get; } = employeeId;
        public string FullName { get; } = fullName;
        public string Code { get; } = code;
        public string Position { get; } = position;
        public decimal HdMM { get; set; }
        public decimal ActualMM { get; set; }
        public decimal LogworkHours { get; set; }
        public Dictionary<DateOnly, decimal> LogDaily { get; } = new();
    }

    // ─── defect / leakage ────────────────────────────────────────────────────

    public async Task<QualityDto> QualityAsync(Guid projectId, string? kind, string? year, CancellationToken ct)
    {
        var scope = await access.OpenAsync(projectId, ct);
        return await QualityDataAsync(scope, kind == QualityKinds.Leakage ? QualityKinds.Leakage : QualityKinds.Defect, year, ct);
    }

    /// <summary>
    /// PUT /projects/{id}/quality — { kind, unit, year, entries: [{ month, defects, productSize }] }.
    /// Empty defects removes a month already entered (and skips a new one). Needs project.manage or report.quality.
    /// </summary>
    public async Task<QualityDto> SaveQualityAsync(Guid projectId, SaveQualityRequest r, CancellationToken ct)
    {
        var scope = await access.OpenAsync(projectId, ct);
        if (!scope.CanManage && !user.Has(Permissions.ReportQuality)) throw new ForbiddenException();
        var p = scope.Project;
        var kind = r.Kind == QualityKinds.Leakage ? QualityKinds.Leakage : QualityKinds.Defect;
        var (from, to) = await MonthRangeAsync(p, ct);
        var entries = r.Entries ?? [];
        var errors = new ValidationErrors();
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var month = e.Month ?? string.Empty;
            if (!IsMonth(month) || string.CompareOrdinal(month, from) < 0 || string.CompareOrdinal(month, to) > 0) errors.Add($"entries.{i}.month", "validation.invalid_value");
            if (!IsBlank(e.Defects)) errors.Number($"entries.{i}.defects", e.Defects, 0, 100000, integer: true);
            if (!IsBlank(e.ProductSize)) errors.Number($"entries.{i}.productSize", e.ProductSize, 0, 1000000);
        }
        var unit = Text.Clean(r.Unit);
        if (unit.Length > 0 && !ProductSizeUnits.Contains(unit)) errors.Add("unit", "validation.invalid_value");
        errors.ThrowIfAny();

        var existing = await db.Set<ProjectQualityMetric>().Where(q => q.ProjectId == p.Id && q.Kind == kind).ToListAsync(ct);
        var defaultUnit = existing.OrderByDescending(q => q.PeriodMonth).FirstOrDefault()?.ProductSizeUnit ?? "Point";
        if (unit.Length > 0) foreach (var q in existing) q.ProductSizeUnit = unit;
        foreach (var e in entries)
        {
            var month = MonthStart(e.Month!);
            var row = existing.FirstOrDefault(q => q.PeriodMonth == month);
            if (IsBlank(e.Defects))
            {
                if (row is not null) db.Set<ProjectQualityMetric>().Remove(row);
                continue;
            }
            if (row is null)
            {
                row = new ProjectQualityMetric { ProjectId = p.Id, Kind = kind, PeriodMonth = month };
                db.Set<ProjectQualityMetric>().Add(row);
                existing.Add(row);
            }
            row.DefectCount = (int)Number(e.Defects);
            row.ProductSize = IsBlank(e.ProductSize) ? 0 : Number(e.ProductSize);
            row.ProductSizeUnit = unit.Length > 0 ? unit : row.ProductSizeUnit ?? defaultUnit;
        }
        await db.SaveChangesAsync(ct);
        var year = r.Year is { ValueKind: JsonValueKind.Number } y ? y.GetRawText() : r.Year is { ValueKind: JsonValueKind.String } ys ? ys.GetString() : null;
        return await QualityDataAsync(scope, kind, year, ct);
    }

    /// <summary>
    /// Defect (kind defect) or leakage rates per month of one year plus year-to-date:
    ///   ratePerSize = defects ÷ product size (e.g. KLOC); ratePerMM = defects ÷ billable MM, where
    ///   billable MM of a month = Σ actual_effort × allocation_rate / 100 of the project (Effort grid) — not × 100
    ///   year-to-date only adds the months entered (a month without data adds no billable MM either)
    ///   a zero denominator gives null. Rates are rounded to 4 decimals. Example: 12 defects, 6 MM → 2 defects / MM.
    /// Months that can be entered: project start month … min(project end month, current month).
    /// </summary>
    private async Task<QualityDto> QualityDataAsync(ProjectScope scope, string kind, string? yearParam, CancellationToken ct)
    {
        var p = scope.Project;
        var (from, to) = await MonthRangeAsync(p, ct);
        var years = Enumerable.Range(int.Parse(from[..4]), Math.Max(0, int.Parse(to[..4]) - int.Parse(from[..4]) + 1)).ToList();
        var nowYear = int.Parse((await clock.TodayAsync(ct)).ToString("yyyy"));
        var year = int.TryParse(yearParam, out var y) && years.Contains(y) ? y : years.Count > 0 ? years[^1] : nowYear;
        var months = Enumerable.Range(1, 12).Select(m => $"{year}-{m:D2}")
            .Where(k => string.CompareOrdinal(k, from) >= 0 && string.CompareOrdinal(k, to) <= 0).ToList();

        var metrics = await db.Set<ProjectQualityMetric>().AsNoTracking().Where(q => q.ProjectId == p.Id && q.Kind == kind).ToListAsync(ct);
        var billable = (await db.Set<EffortAllocation>().AsNoTracking().Where(a => a.ProjectId == p.Id)
                .Select(a => new { a.PeriodMonth, a.ActualEffort, a.AllocationRate }).ToListAsync(ct))
            .GroupBy(a => ProjectDates.MonthKey(a.PeriodMonth))
            .ToDictionary(g => g.Key, g => ProjectDates.Round2(g.Sum(a => a.ActualEffort * a.AllocationRate / 100m)));
        var t = await QualityThresholds.LoadAsync(rules, ct);
        static decimal? Ratio(decimal a, decimal b) => b > 0 ? Math.Round(a / b, 4, MidpointRounding.AwayFromZero) : null;

        int ytdDefects = 0;
        decimal ytdSize = 0, ytdBillable = 0;
        var rows = months.Select(month =>
        {
            var entry = metrics.FirstOrDefault(q => ProjectDates.MonthKey(q.PeriodMonth) == month);
            var defects = entry?.DefectCount ?? 0;
            var size = entry?.ProductSize ?? 0;
            var mm = billable.GetValueOrDefault(month);
            if (entry is not null)
            {
                ytdDefects += defects;
                ytdSize += size;
                ytdBillable += mm;
            }
            var perMM = entry is null ? null : Ratio(defects, mm);
            var perMMYtd = Ratio(ytdDefects, ytdBillable);
            return new QualityRowDto(month, entry is not null, defects, size, entry?.ProductSizeUnit ?? "KLOC", mm,
                entry is null ? null : Ratio(defects, size), perMM, t.RateTone(perMM, kind),
                ytdDefects, ProjectDates.Round2(ytdSize), ProjectDates.Round2(ytdBillable),
                Ratio(ytdDefects, ytdSize), perMMYtd, t.RateTone(perMMYtd, kind));
        }).ToList();
        var thresholds = kind == QualityKinds.Defect
            ? new RateThresholdsDto(t.DefectGoodMin, t.DefectGoodMax, t.DefectWarnMax)
            : new RateThresholdsDto(t.LeakageGoodMin, t.LeakageGoodMax, t.LeakageWarnMax);
        // Product size unit chosen for the project (Point / Request / Module / KLOC): the latest record's.
        var unit = metrics.OrderByDescending(q => q.PeriodMonth).FirstOrDefault()?.ProductSizeUnit ?? "Point";
        var canEdit = (scope.CanManage || user.Has(Permissions.ReportQuality)) && !p.IsDeleted;
        return new QualityDto(kind, year, years, from, to, unit, rows, thresholds, canEdit);
    }

    private async Task<(string From, string To)> MonthRangeAsync(Project p, CancellationToken ct)
    {
        var now = ProjectDates.MonthKey(await clock.TodayAsync(ct));
        var from = p.StartDate is { } s ? ProjectDates.MonthKey(s) : $"{now[..4]}-01";
        var end = p.EndDate is { } e ? ProjectDates.MonthKey(e) : now;
        return (from, string.CompareOrdinal(end, now) < 0 ? end : now);
    }

    public static bool IsMonth(string? value)
        => value is { Length: 7 } && DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _);

    private static DateOnly MonthStart(string month) => DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static bool IsBlank(JsonElement? e)
        => e is not { } v || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || (v.ValueKind == JsonValueKind.String && v.GetString()!.Length == 0);

    private static decimal Number(JsonElement? e)
        => e!.Value.ValueKind == JsonValueKind.Number ? e.Value.GetDecimal() : decimal.Parse(e.Value.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
}
