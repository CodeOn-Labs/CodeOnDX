using Dx.Application.Common;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Work;

public record AttendanceDto(
    Guid? Id, DateOnly Date, string? CheckIn, string? CheckOut, string? Source, string? Note, bool Late, bool MissingOut, string? UpdatedBy);

/// <summary>Cell of the month grid: the record (if any) + duration type of an approved leave that day (full_day, half_day_am…).</summary>
public sealed record AttendanceCellDto : AttendanceDto
{
    public AttendanceCellDto(AttendanceDto record, string? onLeave) : base(record) => OnLeave = onLeave;

    public string? OnLeave { get; init; }
}

public sealed record AttendanceSummaryDto(int WorkedDays, int LateCount, int MissingOutCount, decimal LeaveDays);

public sealed record AttendanceRowDto(WorkEmployeeRefDto Employee, IReadOnlyDictionary<string, AttendanceCellDto> Records, AttendanceSummaryDto Summary);

public sealed record AttendanceDayDto(DateOnly Date, bool Weekend, string? Holiday);

public sealed record AttendanceMonthDto(
    string Month, IReadOnlyList<AttendanceDayDto> Days, string WorkStartTime, string WorkEndTime, decimal GraceMinutes, decimal EditLimitDays,
    bool CanEditAll, Guid? MyEmployeeId, IReadOnlyList<AttendanceRowDto> Items, int Page, int PageSize, int TotalCount);

public sealed record AttendanceTodayDto(DateOnly Date, string Now, bool AllowAppCheckIn, AttendanceDto? Record, string WorkStartTime);

public sealed record AttendanceFilter(string? Month, string? Search, IReadOnlyCollection<Guid> DepartmentIds, bool OnlyIssues);

public sealed record SaveAttendanceRequest(string? EmployeeId, string? Date, string? CheckIn, string? CheckOut, string? Note);

public sealed record ImportAttendanceRow(string? EmployeeCode, string? Date, string? CheckIn, string? CheckOut);

public sealed record ImportAttendanceRequest(IReadOnlyList<ImportAttendanceRow>? Rows, bool? Overwrite);

public sealed record ImportAttendanceError(int Row, string EmployeeCode, IReadOnlyList<string> Codes);

public sealed record ImportAttendanceResult(int Imported, int Skipped, IReadOnlyList<ImportAttendanceError> Errors);

/// <summary>
/// Check-in / check-out (D4): the Vào / Ra buttons (time taken from the server clock, tenant
/// time zone), manual corrections and the time-clock file import. Late = check-in after
/// work_start_time + ATTENDANCE_LATE_GRACE_MINUTES (e.g. 08:30 + 15 → after 08:45 is late).
/// </summary>
public sealed class AttendanceService(
    IAppDbContext db, ICurrentUser user, WorkScope scope, RuleService rules, SettingService settings, ITenantClock clock)
{
    public const string SourceApp = "app";
    public const string SourceManual = "manual";
    public const string SourceImport = "import";

    /// <summary>GET /attendance?month=&amp;search=&amp;departmentIds=&amp;onlyIssues=&amp;page=&amp;pageSize= (default 50 people).</summary>
    public async Task<AttendanceMonthDto> MonthAsync(AttendanceFilter f, PageRequest paging, CancellationToken ct)
    {
        if (!user.Has(Permissions.TimesheetEdit) && !user.Has(Permissions.TimesheetAllView) && !user.Has(Permissions.TimesheetAllEdit))
            throw new ForbiddenException();
        var today = await clock.TodayAsync(ct);
        var month = f.Month ?? today.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var first))
            throw AppValidationException.Single("month", "validation.invalid_value");
        var last = first.AddMonths(1).AddDays(-1);
        var all = user.Has(Permissions.TimesheetAllView) || user.Has(Permissions.TimesheetAllEdit);
        var me = await scope.MyEmployeeIdAsync(ct);

        var query = db.Set<Employee>().AsNoTracking().Where(e => e.Status.Code != MasterDataCodes.EmployeeStatusResigned);
        if (!all) query = query.Where(e => e.Id == me);
        if (f.DepartmentIds.Count > 0) query = query.Where(e => e.DepartmentId != null && f.DepartmentIds.Contains(e.DepartmentId.Value));
        var people = (await query.ToListAsync(ct)).Where(e => Text.Matches(f.Search, e.FullName, e.Code)).OrderBy(e => e.Code, StringComparer.Ordinal).ToList();
        var ids = people.Select(e => e.Id).ToList();

        var ctx = await ContextAsync(ct);
        var records = await RecordsAsync(db.Set<AttendanceRecord>().Where(a => ids.Contains(a.EmployeeId) && a.WorkDate >= first && a.WorkDate <= last), ctx, today, ct);
        var leaves = await db.Set<LeaveRequest>().AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.Status == "approved" && r.FromDate <= last && r.ToDate >= first)
            .Select(r => new { r.EmployeeId, r.FromDate, r.ToDate, r.DurationType }).ToListAsync(ct);
        var holidays = await db.Set<Holiday>().AsNoTracking().Where(h => h.HolidayDate >= first && h.HolidayDate <= last).ToDictionaryAsync(h => h.HolidayDate, h => h.Name, ct);
        var days = WorkDates.Range(first, last).ToList();

        var rows = people.Select(e =>
        {
            var map = new Dictionary<string, AttendanceCellDto>();
            int late = 0, missingOut = 0, worked = 0;
            decimal leave = 0;
            foreach (var d in days)
            {
                var rec = records.GetValueOrDefault((e.Id, d));
                var lv = leaves.FirstOrDefault(r => r.EmployeeId == e.Id && r.FromDate <= d && r.ToDate >= d);
                if (rec?.Late == true) late++;
                if (rec?.MissingOut == true) missingOut++;
                if (rec?.CheckIn is not null) worked++;
                if (lv is not null && !WorkDates.IsWeekend(d) && !holidays.ContainsKey(d)) leave += lv.DurationType == "full_day" ? 1 : 0.5m;
                if (rec is not null || lv is not null)
                    map[WorkDates.Iso(d)] = new AttendanceCellDto(rec ?? new AttendanceDto(null, d, null, null, null, null, false, false, null), lv?.DurationType);
            }
            return new AttendanceRowDto(WorkScope.Ref(e), map, new AttendanceSummaryDto(worked, late, missingOut, leave));
        }).Where(r => !f.OnlyIssues || r.Summary.LateCount + r.Summary.MissingOutCount > 0).ToList();

        var page = Paging.Of(rows, paging);
        var set = await rules.SnapshotAsync(ct);
        return new AttendanceMonthDto(month, days.Select(d => new AttendanceDayDto(d, WorkDates.IsWeekend(d), holidays.GetValueOrDefault(d))).ToList(),
            WorkDates.Hm(ctx.WorkStart), ctx.WorkEnd, ctx.Grace, set.Number(RuleKeys.AttendanceEditLimitDays, 7),
            user.Has(Permissions.TimesheetAllEdit), me, page.Items, page.Page, page.PageSize, page.TotalCount);
    }

    /// <summary>GET /attendance/today — own record of today and whether the Vào / Ra buttons are available.</summary>
    public async Task<AttendanceTodayDto> TodayAsync(CancellationToken ct)
    {
        var me = await scope.MyEmployeeIdAsync(ct);
        var now = await clock.LocalNowAsync(ct);
        var today = DateOnly.FromDateTime(now);
        var ctx = await ContextAsync(ct);
        AttendanceDto? record = null;
        if (me is { } m)
            record = (await RecordsAsync(db.Set<AttendanceRecord>().Where(a => a.EmployeeId == m && a.WorkDate == today), ctx, today, ct)).Values.FirstOrDefault();
        return new AttendanceTodayDto(today, now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), ctx.AllowApp && me is not null, record, WorkDates.Hm(ctx.WorkStart));
    }

    /// <summary>POST /attendance/check-in | check-out — server time (the browser clock is not trusted).</summary>
    public async Task<(AttendanceDto Dto, string Time)> CheckAsync(bool checkIn, CancellationToken ct)
    {
        var me = await scope.RequireMyEmployeeIdAsync(ct);
        var ctx = await ContextAsync(ct);
        if (!ctx.AllowApp) throw new ConflictException("attendance_check.err_disabled");
        var now = await clock.LocalNowAsync(ct);
        var today = DateOnly.FromDateTime(now);
        var time = new TimeOnly(now.Hour, now.Minute);
        var row = await db.Set<AttendanceRecord>().FirstOrDefaultAsync(a => a.EmployeeId == me && a.WorkDate == today, ct);
        if (checkIn && row?.CheckIn is not null) throw new ConflictException("attendance_check.err_already_in");
        if (!checkIn && row?.CheckIn is null) throw new ConflictException("attendance_check.err_not_in");
        if (row is null)
        {
            row = new AttendanceRecord { EmployeeId = me, WorkDate = today, Source = SourceApp };
            db.Set<AttendanceRecord>().Add(row);
        }
        if (checkIn) (row.CheckIn, row.Source) = (time, SourceApp);
        // Check-out in the same minute as check-in still has to be after it.
        else row.CheckOut = time > row.CheckIn!.Value ? time : row.CheckIn.Value.AddMinutes(1);
        await db.SaveChangesAsync(ct);
        var dto = (await RecordsAsync(db.Set<AttendanceRecord>().Where(a => a.Id == row.Id), ctx, today, ct)).Values.Single();
        return (dto, WorkDates.Hm(time));
    }

    /// <summary>
    /// PUT /attendance — manual correction. Someone else's day needs timesheet.all_edit and a note;
    /// an employee may complete their own day within ATTENDANCE_EDIT_LIMIT_DAYS days but not change
    /// times already recorded. No future dates.
    /// </summary>
    public async Task<AttendanceDto> SaveAsync(SaveAttendanceRequest r, CancellationToken ct)
    {
        var me = await scope.MyEmployeeIdAsync(ct);
        var errors = new ValidationErrors();
        var employeeId = Guid.TryParse(r.EmployeeId, out var eid) ? eid : me ?? throw new ConflictException("work_common.err_no_employee");
        var date = errors.Date("date", r.Date);
        var (checkIn, checkOut) = ValidateTimes(errors, r.CheckIn, r.CheckOut);
        var note = Text.Clean(r.Note);
        errors.MaxLength("note", note, 300);
        var self = employeeId == me;
        if (!self && !user.Has(Permissions.TimesheetAllEdit)) throw new ForbiddenException();
        if (!self && note.Length == 0) errors.Add("note", "attendance_save.err_note_required");
        var today = await clock.TodayAsync(ct);
        if (date is { } d0 && d0 > today) errors.Add("date", "attendance_save.err_future");
        var existing = date is { } d1 ? await db.Set<AttendanceRecord>().FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == d1, ct) : null;
        if (self && !user.Has(Permissions.TimesheetAllEdit) && date is { } d2)
        {
            var limit = await rules.GetNumberAsync(RuleKeys.AttendanceEditLimitDays, 7, ct);
            if (d2 < today.AddDays(-(int)limit)) errors.Add("date", "attendance_save.err_edit_limit", new() { ["days"] = limit });
            if (existing?.CheckIn is { } ci && checkIn != ci) errors.Add("checkIn", "attendance_save.err_self_overwrite");
            if (existing?.CheckOut is { } co && checkOut != co) errors.Add("checkOut", "attendance_save.err_self_overwrite");
        }
        errors.ThrowIfAny();
        if (!await db.Set<Employee>().AnyAsync(e => e.Id == employeeId, ct)) throw new NotFoundException();

        if (existing is null)
        {
            existing = new AttendanceRecord { EmployeeId = employeeId, WorkDate = date!.Value };
            db.Set<AttendanceRecord>().Add(existing);
        }
        existing.CheckIn = checkIn;
        existing.CheckOut = checkOut;
        existing.Source = SourceManual;
        existing.Note = note; // NOT NULL for manual rows (V007); "" is shown as null
        await db.SaveChangesAsync(ct);
        var ctx = await ContextAsync(ct);
        return (await RecordsAsync(db.Set<AttendanceRecord>().Where(a => a.Id == existing.Id), ctx, today, ct)).Values.Single();
    }

    /// <summary>
    /// POST /attendance/import — rows read from the time-clock file by the browser, matched by
    /// employee code. Row errors are codes the FE translates: unknown_employee, invalid_date,
    /// invalid_time, out_before_in. Days corrected by hand are kept unless overwrite = true.
    /// </summary>
    public async Task<ImportAttendanceResult> ImportAsync(ImportAttendanceRequest r, CancellationToken ct)
    {
        var rows = (r.Rows ?? []).Take(5000).ToList();
        var employees = await db.Set<Employee>().AsNoTracking().ToDictionaryAsync(e => e.Code, e => e.Id, StringComparer.Ordinal, ct);
        var imported = 0;
        var skipped = 0;
        var errors = new List<ImportAttendanceError>();
        var touched = new Dictionary<(Guid, DateOnly), AttendanceRecord>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var code = Text.Clean(row.EmployeeCode);
            var codes = new List<string>();
            if (!employees.TryGetValue(code.ToUpperInvariant(), out var employeeId)) codes.Add("unknown_employee");
            var date = WorkDates.Parse(row.Date);
            if (date is null) codes.Add("invalid_date");
            var inText = Text.Clean(row.CheckIn);
            var outText = Text.Clean(row.CheckOut);
            var checkIn = RequestService.ParseTime(inText);
            var checkOut = RequestService.ParseTime(outText);
            if ((inText.Length > 0 && checkIn is null) || (outText.Length > 0 && checkOut is null)) codes.Add("invalid_time");
            else if (checkIn is { } a && checkOut is { } b && b <= a) codes.Add("out_before_in");
            if (codes.Count > 0)
            {
                errors.Add(new ImportAttendanceError(i + 1, code, codes));
                continue;
            }
            var key = (employeeId, date!.Value);
            if (!touched.TryGetValue(key, out var existing))
                existing = await db.Set<AttendanceRecord>().FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.WorkDate == date, ct);
            if (existing is not null && existing.Source == SourceManual && r.Overwrite != true)
            {
                skipped++;
                continue;
            }
            if (existing is null)
            {
                existing = new AttendanceRecord { EmployeeId = employeeId, WorkDate = date.Value };
                db.Set<AttendanceRecord>().Add(existing);
            }
            (existing.CheckIn, existing.CheckOut, existing.Source, existing.Note) = (checkIn, checkOut, SourceImport, null);
            touched[key] = existing;
            imported++;
        }
        await db.SaveChangesAsync(ct);
        return new ImportAttendanceResult(imported, skipped, errors);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private sealed record Context(TimeOnly WorkStart, string WorkEnd, decimal Grace, bool AllowApp);

    private async Task<Context> ContextAsync(CancellationToken ct)
    {
        var s = await settings.GetAllAsync(ct);
        var set = await rules.SnapshotAsync(ct);
        var start = RequestService.ParseTime(s.GetValueOrDefault("work_start_time")) ?? new TimeOnly(8, 30);
        return new Context(start, s.GetValueOrDefault("work_end_time") ?? "17:30",
            set.Enabled(RuleKeys.AttendanceLateGraceMinutes) ? set.Number(RuleKeys.AttendanceLateGraceMinutes, 15) : 0,
            (s.GetValueOrDefault("allow_app_check_in") ?? "true") == "true");
    }

    /// <summary>
    /// DTOs keyed by (employee, date). missingOut = checked in, no check-out, and the day is over.
    /// updatedBy = who corrected / imported the day (not shown for the employee's own app check-in).
    /// </summary>
    private async Task<Dictionary<(Guid, DateOnly), AttendanceDto>> RecordsAsync(IQueryable<AttendanceRecord> query, Context ctx, DateOnly today, CancellationToken ct)
    {
        var rows = await query.AsNoTracking().ToListAsync(ct);
        var userIds = rows.Where(a => a.Source != SourceApp && a.UpdatedBy != null).Select(a => a.UpdatedBy!.Value).Distinct().ToList();
        var names = await db.Set<User>().AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var lateAfter = ctx.WorkStart.AddMinutes((double)ctx.Grace);
        return rows.ToDictionary(a => (a.EmployeeId, a.WorkDate), a => new AttendanceDto(
            a.Id, a.WorkDate, a.CheckIn is { } i ? WorkDates.Hm(i) : null, a.CheckOut is { } o ? WorkDates.Hm(o) : null, a.Source,
            string.IsNullOrEmpty(a.Note) ? null : a.Note,
            a.CheckIn is { } ci && ci > lateAfter,
            a.CheckIn is not null && a.CheckOut is null && a.WorkDate < today,
            a.Source != SourceApp && a.UpdatedBy is { } by ? names.GetValueOrDefault(by) : null));
    }

    private static (TimeOnly? In, TimeOnly? Out) ValidateTimes(ValidationErrors errors, string? inRaw, string? outRaw)
    {
        var inText = Text.Clean(inRaw);
        var outText = Text.Clean(outRaw);
        var checkIn = RequestService.ParseTime(inText);
        var checkOut = RequestService.ParseTime(outText);
        if (inText.Length > 0 && checkIn is null) errors.Add("checkIn", "validation.invalid_time");
        if (outText.Length > 0 && checkOut is null) errors.Add("checkOut", "validation.invalid_time");
        if (checkIn is { } a && checkOut is { } b && b <= a) errors.Add("checkOut", "attendance_save.err_out_before_in");
        return (checkIn, checkOut);
    }
}
