using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dx.Application.Common;
using Dx.Application.Work;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Projects;

public sealed record MonthlyReportHeaderDto(string ClientName, string CompanyName, string OrderNo, string WorkName, string Responsible, string Language);

public sealed record MonthlyReportColumnDto(string Id, string Label, string LabelJa, string Type, string? Source);

public sealed record MonthlyReportDayDto(
    DateOnly Date, bool Weekend, decimal? Hours, string? HoursSource, bool HasLog, string? Start, string? End, string? TimeSource,
    string Content, IReadOnlyDictionary<string, string> Custom);

public sealed record MonthlyReportMemberDto(Guid EmployeeId, string FullName, string Code, decimal TotalHours, IReadOnlyList<MonthlyReportDayDto> Days);

public sealed record MonthlyReportDto(
    Guid? Id, string Month, bool IsDraft, bool CanEdit, MonthlyReportHeaderDto Header, IReadOnlyList<MonthlyReportColumnDto> Columns,
    IReadOnlyList<MonthlyReportMemberDto> Members, int ApprovedLogDays, int ManualHourDays, int ManualTimeDays, DateTime? UpdatedAt, string? UpdatedByName);

public sealed record MonthlyReportSavedDto(DateTime UpdatedAt);

public sealed record MonthlyReportColumnCreatedDto(string Id);

public sealed record ReportHeaderRequest(string? Field, JsonElement? Value);

public sealed record ReportCellRequest(string? EmployeeId, string? Date, string? Field, JsonElement? Value);

public sealed record ReportSyncRequest(string? Kind, string? EmployeeId, string? Date);

public sealed record ReportColumnRequest(string? Label, string? LabelJa, string? Type, JsonElement? Position);

public sealed record StatusReportItemDto(string Metric, string Content);

public sealed record StatusReportDto(string Status, IReadOnlyList<StatusReportItemDto> Items, DateTime? UpdatedAt, string? UpdatedByName);

public sealed record StatusReportFillDto(string Status, IReadOnlyList<StatusReportItemDto> Items);

public sealed record StatusReportItemRequest(string? Metric, string? Content);

public sealed record SaveStatusReportRequest(string? Status, IReadOnlyList<StatusReportItemRequest>? Items);

/// <summary>
/// Monthly report (Japanese-style work report per project and month) and the project status
/// report ("Report" button). Anyone who may open the project edits the monthly report, as in
/// the original product; the status report needs project.manage.
/// </summary>
public sealed class MonthlyReportService(IAppDbContext db, ICurrentUser user, ProjectAccess access, Settings.SettingService settings, ITenantClock clock)
{
    private static readonly (string Key, string Label, string LabelJa, string Type, string Source)[] DefaultColumns =
    [
        ("date", "Ngày", "日付", "text", "date"),
        ("start", "Bắt đầu", "開始", "text", "check_in"),
        ("end", "Kết thúc", "終了", "text", "check_out"),
        ("hours", "Số giờ", "作業時間", "number", "hours"),
        ("content", "Nội dung công việc", "作業内容", "text", "content"),
    ];

    private static readonly Dictionary<string, (string Column, int Max)> HeaderFields = new()
    {
        ["clientName"] = ("client_name", 200), ["companyName"] = ("company_name", 200), ["orderNo"] = ("order_no", 100),
        ["workName"] = ("work_name", 200), ["responsible"] = ("responsible", 200),
    };

    private static readonly string[] FullTemplate = ["Tiến độ", "Chất lượng", "Nhân sự", "Kỹ thuật", "Giao tiếp", "Báo cáo kết thúc", "Theo dõi issue/risk cũ"];
    private static readonly string[] ReportStatuses = ["GOOD", "RISK", "SOS"];

    // ─── monthly report ──────────────────────────────────────────────────────

    /// <summary>
    /// GET /projects/{id}/monthly-reports/{yyyy-MM}. Members = people with an assignment period
    /// overlapping the month (minus those removed). Hours of a day = approved work-log hours on the
    /// project, start / end = check-in / check-out — unless the cell was edited by hand
    /// (source manual). A person's total = Σ hours of the month. A month never edited shows a draft.
    /// </summary>
    public async Task<MonthlyReportDto> GetAsync(Guid projectId, string month, CancellationToken ct)
    {
        var scope = await access.OpenAsync(projectId, ct);
        var p = scope.Project;
        var (first, last) = Range(month);
        var report = await ReportAsync(p.Id, first, ct, tracking: false);
        var draft = report is null ? await DraftAsync(p, first, ct) : null;
        var members = await MembersAsync(p.Id, first, last, report?.Id, ct);
        var approved = (await db.Set<TimesheetEntry>().AsNoTracking()
                .Where(t => t.ProjectId == p.Id && t.Status == "approved" && t.WorkDate >= first && t.WorkDate <= last)
                .GroupBy(t => new { t.EmployeeId, t.WorkDate }).Select(g => new { g.Key.EmployeeId, g.Key.WorkDate, Hours = g.Sum(t => t.Hours) })
                .ToListAsync(ct))
            .ToDictionary(x => (x.EmployeeId, x.WorkDate), x => x.Hours);
        var memberIds = members.Select(m => m.Id).ToList();
        var attendance = (await db.Set<AttendanceRecord>().AsNoTracking()
                .Where(a => memberIds.Contains(a.EmployeeId) && a.WorkDate >= first && a.WorkDate <= last).ToListAsync(ct))
            .ToDictionary(a => (a.EmployeeId, a.WorkDate));
        var entries = report is null ? [] : (await db.Set<MonthlyReportEntry>().AsNoTracking().Where(e => e.ReportId == report.Id).ToListAsync(ct))
            .ToDictionary(e => (e.EmployeeId, e.WorkDate));
        var columns = report is null ? DefaultColumnDtos() : await ColumnsAsync(report.Id, ct);

        int manualHours = 0, manualTimes = 0;
        var days = WorkDates.Range(first, last).ToList();
        var memberDtos = members.Select(e =>
        {
            decimal total = 0;
            var dayDtos = days.Select(d =>
            {
                var c = entries.GetValueOrDefault((e.Id, d));
                var att = attendance.GetValueOrDefault((e.Id, d));
                var hoursManual = c?.HoursSource == "manual";
                var timeManual = c?.TimeSource == "manual";
                if (hoursManual) manualHours++;
                if (timeManual) manualTimes++;
                var hasLog = approved.TryGetValue((e.Id, d), out var logged);
                var hours = hoursManual ? c!.Hours : hasLog ? WorkDates.Round2(logged) : null;
                total += hours ?? 0;
                return new MonthlyReportDayDto(d, WorkDates.IsWeekend(d), hours, hoursManual ? "manual" : hasLog ? "timesheet" : null, hasLog,
                    timeManual ? Hm(c!.StartTime) : Hm(att?.CheckIn), timeManual ? Hm(c!.EndTime) : Hm(att?.CheckOut),
                    timeManual ? "manual" : att is not null ? "attendance" : null, c?.Content ?? string.Empty, Custom(c?.CustomValues));
            }).ToList();
            return new MonthlyReportMemberDto(e.Id, e.FullName, e.Code, WorkDates.Round2(total), dayDtos);
        }).ToList();

        var header = report is not null
            ? new MonthlyReportHeaderDto(report.ClientName ?? string.Empty, report.CompanyName ?? string.Empty, report.OrderNumber ?? string.Empty,
                report.WorkName ?? string.Empty, report.ResponsiblePerson ?? string.Empty, report.Language)
            : draft!;
        var updatedBy = report?.UpdatedBy is { } ub ? await db.Set<User>().AsNoTracking().Where(u => u.Id == ub).Select(u => u.FullName).FirstOrDefaultAsync(ct) : null;
        return new MonthlyReportDto(report?.Id, month, report is null, !p.IsDeleted, header, columns, memberDtos, approved.Count, manualHours, manualTimes,
            report?.UpdatedAt, updatedBy);
    }

    /// <summary>PATCH …/header — { field: clientName | companyName | orderNo | workName | responsible | language, value }.</summary>
    public async Task<MonthlyReportSavedDto> UpdateHeaderAsync(Guid projectId, string month, ReportHeaderRequest r, CancellationToken ct)
    {
        var p = await WritableAsync(projectId, ct);
        var field = Text.Clean(r.Field);
        var value = Str(r.Value);
        var errors = new ValidationErrors();
        if (field == "language") { if (value is not ("vi" or "ja")) errors.Add("value", "validation.invalid_value"); }
        else if (!HeaderFields.TryGetValue(field, out var def)) errors.Add("field", "validation.invalid_value");
        else errors.MaxLength("value", value, def.Max);
        errors.ThrowIfAny();
        var report = await EnsureReportAsync(p, month, ct);
        switch (field)
        {
            case "language": report.Language = value; break;
            case "clientName": report.ClientName = value; break;
            case "companyName": report.CompanyName = value; break;
            case "orderNo": report.OrderNumber = value; break;
            case "workName": report.WorkName = value; break;
            default: report.ResponsiblePerson = value; break;
        }
        await TouchAndSaveAsync(report, ct);
        return new MonthlyReportSavedDto(report.UpdatedAt);
    }

    /// <summary>
    /// PATCH …/cells — { employeeId, date, field: hours | start | end | content | custom:&lt;column&gt;, value }.
    /// Hours 0–24 in steps of 0.5; an edited hours / time cell is marked manual so syncing does not
    /// overwrite it. Editing only start (or only end) keeps the other time from attendance.
    /// </summary>
    public async Task<MonthlyReportSavedDto> UpdateCellAsync(Guid projectId, string month, ReportCellRequest r, CancellationToken ct)
    {
        var p = await WritableAsync(projectId, ct);
        var (first, last) = Range(month);
        var errors = new ValidationErrors();
        var members = await MembersAsync(p.Id, first, last, null, ct);
        var employeeId = Guid.TryParse(r.EmployeeId, out var eid) && members.Any(m => m.Id == eid) ? eid : Guid.Empty;
        if (employeeId == Guid.Empty) errors.Add("employeeId", "validation.invalid_value");
        var date = WorkDates.Parse(r.Date);
        if (date is not { } d0 || d0 < first || d0 > last) errors.Add("date", "validation.invalid_value");
        var field = Text.Clean(r.Field);
        var isBlank = r.Value is null || r.Value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                      || (r.Value.Value.ValueKind == JsonValueKind.String && r.Value.Value.GetString()!.Length == 0);
        var text = r.Value is { ValueKind: JsonValueKind.Number } n ? n.GetRawText() : Str(r.Value);
        decimal? hours = null;
        TimeOnly? time = null;
        string? customKey = null;
        var report0 = await ReportAsync(p.Id, first, ct, tracking: false);
        var columns = report0 is null ? DefaultColumnDtos() : await ColumnsAsync(report0.Id, ct);
        switch (field)
        {
            case "hours":
                if (!isBlank)
                {
                    hours = errors.Number("value", r.Value, 0, 24);
                    if (hours is { } h && h * 2 != decimal.Truncate(h * 2)) errors.Add("value", "monthly_report_cell.err_hours_step");
                }
                break;
            case "start" or "end":
                if (!isBlank && (time = RequestService.ParseTime(text)) is null) errors.Add("value", "validation.invalid_time");
                break;
            case "content":
                errors.MaxLength("value", text, 1000);
                break;
            default:
                if (field.StartsWith("custom:", StringComparison.Ordinal) && columns.FirstOrDefault(c => c.Id == field[7..] && c.Source is null) is { } col)
                {
                    customKey = col.Id;
                    if (col.Type == "number" && !isBlank && !decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _))
                        errors.Add("value", "validation.invalid_number");
                    else errors.MaxLength("value", text, 500);
                }
                else errors.Add("field", "validation.invalid_value");
                break;
        }
        errors.ThrowIfAny();

        var report = await EnsureReportAsync(p, month, ct);
        var cell = await db.Set<MonthlyReportEntry>().FirstOrDefaultAsync(e => e.ReportId == report.Id && e.EmployeeId == employeeId && e.WorkDate == date, ct);
        if (cell is null)
        {
            cell = new MonthlyReportEntry { ReportId = report.Id, EmployeeId = employeeId, WorkDate = date!.Value, CustomValues = "{}" };
            db.Set<MonthlyReportEntry>().Add(cell);
        }
        AttendanceRecord? att = null;
        if (field is "start" or "end" && cell.TimeSource != "manual")
            att = await db.Set<AttendanceRecord>().AsNoTracking().FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == date, ct);
        switch (field)
        {
            case "hours":
                (cell.Hours, cell.HoursSource) = (hours, "manual");
                break;
            case "start":
                if (cell.TimeSource != "manual") cell.EndTime = att?.CheckOut;
                (cell.StartTime, cell.TimeSource) = (time, "manual");
                break;
            case "end":
                if (cell.TimeSource != "manual") cell.StartTime = att?.CheckIn;
                (cell.EndTime, cell.TimeSource) = (time, "manual");
                break;
            case "content":
                cell.Content = text;
                break;
            default:
                var custom = JsonNode.Parse(string.IsNullOrWhiteSpace(cell.CustomValues) ? "{}" : cell.CustomValues)!.AsObject();
                custom[customKey!] = text;
                cell.CustomValues = custom.ToJsonString();
                break;
        }
        await TouchAndSaveAsync(report, ct);
        return new MonthlyReportSavedDto(report.UpdatedAt);
    }

    /// <summary>
    /// POST …/sync — { kind: hours | time, employeeId?, date? }: drops the manual mark so the cells
    /// take the work-log hours (hours) or the check-in / out times (time) again. Returns the count.
    /// </summary>
    public async Task<(string Kind, int Count)> SyncAsync(Guid projectId, string month, ReportSyncRequest r, CancellationToken ct)
    {
        var p = await WritableAsync(projectId, ct);
        var kind = r.Kind == "time" ? "time" : "hours";
        var (first, _) = Range(month);
        var report = await ReportAsync(p.Id, first, ct, tracking: true);
        var changed = 0;
        if (report is not null)
        {
            var query = db.Set<MonthlyReportEntry>().Where(e => e.ReportId == report.Id);
            if (Guid.TryParse(r.EmployeeId, out var eid))
            {
                var date = WorkDates.Parse(r.Date);
                query = query.Where(e => e.EmployeeId == eid && e.WorkDate == date);
            }
            foreach (var c in await query.ToListAsync(ct))
            {
                if (kind == "hours" && c.HoursSource == "manual") { (c.HoursSource, c.Hours) = ("timesheet", null); changed++; }
                if (kind == "time" && c.TimeSource == "manual") { (c.TimeSource, c.StartTime, c.EndTime) = ("attendance", null, null); changed++; }
            }
            await TouchAndSaveAsync(report, ct);
        }
        return (kind, changed);
    }

    public async Task<MonthlyReportColumnCreatedDto> CreateColumnAsync(Guid projectId, string month, ReportColumnRequest r, CancellationToken ct)
    {
        var p = await WritableAsync(projectId, ct);
        var report = await EnsureReportAsync(p, month, ct);
        var columns = await db.Set<MonthlyReportColumn>().Where(c => c.ReportId == report.Id).OrderBy(c => c.SortOrder).ToListAsync(ct);
        var (label, labelJa, type) = ParseColumn(r, columns, null);
        var position = r.Position is { ValueKind: JsonValueKind.Number } pos && pos.TryGetInt32(out var pv) ? Math.Clamp(pv, 0, columns.Count) : columns.Count;
        var key = "c_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));
        columns.Insert(position, new MonthlyReportColumn { ReportId = report.Id, ColumnKey = key, Label = label, LabelJa = labelJa, DataType = type });
        db.Set<MonthlyReportColumn>().Add(columns[position]);
        for (var i = 0; i < columns.Count; i++) columns[i].SortOrder = i;
        await TouchAndSaveAsync(report, ct);
        return new MonthlyReportColumnCreatedDto(key);
    }

    /// <summary>System columns (with a source) only change their labels, not their type.</summary>
    public async Task UpdateColumnAsync(Guid projectId, string month, string columnKey, ReportColumnRequest r, CancellationToken ct)
    {
        var p = await WritableAsync(projectId, ct);
        var report = await EnsureReportAsync(p, month, ct);
        var columns = await db.Set<MonthlyReportColumn>().Where(c => c.ReportId == report.Id).ToListAsync(ct);
        var col = columns.FirstOrDefault(c => c.ColumnKey == columnKey) ?? throw new NotFoundException();
        var (label, labelJa, type) = ParseColumn(r, columns, col.ColumnKey);
        (col.Label, col.LabelJa) = (label, labelJa);
        if (col.Source is null) col.DataType = type;
        await TouchAndSaveAsync(report, ct);
    }

    public async Task<string> DeleteColumnAsync(Guid projectId, string month, string columnKey, CancellationToken ct)
    {
        var p = await WritableAsync(projectId, ct);
        var report = await EnsureReportAsync(p, month, ct);
        var col = await db.Set<MonthlyReportColumn>().FirstOrDefaultAsync(c => c.ReportId == report.Id && c.ColumnKey == columnKey, ct) ?? throw new NotFoundException();
        if (col.Source is not null) throw new ConflictException("monthly_report_column.err_system");
        db.Set<MonthlyReportColumn>().Remove(col);
        await TouchAndSaveAsync(report, ct);
        return col.Label;
    }

    // ─── status report ───────────────────────────────────────────────────────

    public async Task<StatusReportDto> StatusReportAsync(Guid projectId, CancellationToken ct)
    {
        var p = (await access.ManageAsync(projectId, ct)).Project;
        var row = await db.Set<ProjectStatusReport>().AsNoTracking().FirstOrDefaultAsync(r => r.ProjectId == p.Id, ct);
        if (row is null) return new StatusReportDto("GOOD", FullTemplate.Select(m => new StatusReportItemDto(m, string.Empty)).ToList(), null, null);
        var name = row.UpdatedBy is { } ub ? await db.Set<User>().AsNoTracking().Where(u => u.Id == ub).Select(u => u.FullName).FirstOrDefaultAsync(ct) : null;
        return new StatusReportDto(row.OverallStatus, JsonSerializer.Deserialize<List<StatusReportItemDto>>(row.Items, Json) ?? [], row.UpdatedAt, name);
    }

    public async Task SaveStatusReportAsync(Guid projectId, SaveStatusReportRequest r, CancellationToken ct)
    {
        var p = (await access.ManageAsync(projectId, ct)).Project;
        var errors = new ValidationErrors();
        var status = Text.Clean(r.Status);
        if (!ReportStatuses.Contains(status)) errors.Add("status", "validation.invalid_value");
        var items = r.Items ?? [];
        if (items.Count == 0) errors.Add("items", "project_status_report.err_items_required");
        for (var i = 0; i < items.Count; i++)
        {
            var metric = Text.Clean(items[i].Metric);
            errors.Required($"items.{i}.metric", metric);
            errors.MaxLength($"items.{i}.metric", metric, 100);
            errors.MaxLength($"items.{i}.content", Text.Clean(items[i].Content), 4000);
        }
        errors.ThrowIfAny();
        var row = await db.Set<ProjectStatusReport>().FirstOrDefaultAsync(x => x.ProjectId == p.Id, ct);
        if (row is null)
        {
            row = new ProjectStatusReport { ProjectId = p.Id };
            db.Set<ProjectStatusReport>().Add(row);
        }
        row.OverallStatus = status;
        row.Items = JsonSerializer.Serialize(items.Select(i => new StatusReportItemDto(Text.Clean(i.Metric), Text.Clean(i.Content))), Json);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// POST …/status-report/ai-fill — draft written from the project figures:
    ///   status SOS when more than one overdue milestone or more than one high / critical risk
    ///   (score ≥ 10), RISK when there is one, else GOOD; one paragraph per metric.
    /// </summary>
    public async Task<StatusReportFillDto> AiFillAsync(Guid projectId, CancellationToken ct)
    {
        var p = (await access.ManageAsync(projectId, ct)).Project;
        var today = await clock.TodayAsync(ct);
        var overdue = await db.Set<ProjectMilestone>().AsNoTracking()
            .Where(m => m.ProjectId == p.Id && m.ActualDate == null && m.PlannedDate < today).OrderBy(m => m.PlannedDate).Select(m => m.Name).ToListAsync(ct);
        var risks = (await db.Set<ProjectRisk>().AsNoTracking().Where(r => r.ProjectId == p.Id).ToListAsync(ct))
            .Where(r => ProjectAdminService.IsActive(r.Status)).ToList();
        var high = risks.Where(r => r.Probability * r.Impact >= 10).ToList();
        var status = high.Count > 1 || overdue.Count > 1 ? "SOS" : high.Count > 0 || overdue.Count > 0 ? "RISK" : "GOOD";
        var items = new List<StatusReportItemDto>
        {
            new("Tiến độ", overdue.Count > 0
                ? $"- Đánh giá chung: NG\n- {overdue.Count} mốc đã quá hạn: {string.Join(", ", overdue)}\n- Action: rà soát kế hoạch, thống nhất mốc mới với khách hàng"
                : "- Đánh giá chung: OK\n- Các mốc bàn giao đúng kế hoạch"),
            new("Chất lượng", "- Đánh giá chung: OK\n- Theo dõi Defect/Leakage ở tab KPI"),
            new("Nhân sự", "- Đánh giá chung: OK\n- Xem cảnh báo biến động nhân sự ở tab Tổng quan (nếu có)"),
            new("Rủi ro", risks.Count > 0
                ? $"- {risks.Count} rủi ro đang theo dõi, {high.Count} ở mức Cao/Nghiêm trọng\n{string.Join("\n", high.Select(r => $"- {r.Code} {r.Title}: {r.ResponsePlan ?? string.Empty}"))}"
                : "- Không có rủi ro đang mở"),
        };
        return new StatusReportFillDto(status, items);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<Project> WritableAsync(Guid projectId, CancellationToken ct)
    {
        var p = (await access.OpenAsync(projectId, ct)).Project;
        if (p.IsDeleted) throw new ConflictException("project_detail.err_deleted");
        return p;
    }

    public static (DateOnly First, DateOnly Last) Range(string month)
    {
        var first = DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return (first, first.AddMonths(1).AddDays(-1));
    }

    private Task<MonthlyReport?> ReportAsync(Guid projectId, DateOnly first, CancellationToken ct, bool tracking)
    {
        var q = db.Set<MonthlyReport>().Where(r => r.ProjectId == projectId && r.PeriodMonth == first);
        return (tracking ? q : q.AsNoTracking()).FirstOrDefaultAsync(ct);
    }

    /// <summary>Header of a month never edited: client, company name (Settings), project name, project manager.</summary>
    private async Task<MonthlyReportHeaderDto> DraftAsync(Project p, DateOnly first, CancellationToken ct)
    {
        var client = p.ClientId is { } cid ? await db.Set<Client>().IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking().Where(c => c.Id == cid).Select(c => c.Name).FirstOrDefaultAsync(ct) : null;
        var company = (await settings.GetAllAsync(ct)).GetValueOrDefault("company_name") ?? string.Empty;
        var pm = await db.Set<ProjectAssignment>().AsNoTracking().Where(a => a.ProjectId == p.Id && a.Position.Code == "PM")
            .OrderBy(a => a.CreatedAt).Select(a => a.Employee.FullName).FirstOrDefaultAsync(ct);
        return new MonthlyReportHeaderDto(client ?? string.Empty, company, string.Empty, p.Name, pm ?? string.Empty, "vi");
    }

    /// <summary>The first edit of a month stores the report (draft header + default columns).</summary>
    private async Task<MonthlyReport> EnsureReportAsync(Project p, string month, CancellationToken ct)
    {
        var (first, _) = Range(month);
        var report = await ReportAsync(p.Id, first, ct, tracking: true);
        if (report is not null) return report;
        var draft = await DraftAsync(p, first, ct);
        report = new MonthlyReport
        {
            Id = Guid.CreateVersion7(), ProjectId = p.Id, PeriodMonth = first, ClientName = draft.ClientName, CompanyName = draft.CompanyName,
            OrderNumber = draft.OrderNo, WorkName = draft.WorkName, ResponsiblePerson = draft.Responsible, Language = draft.Language,
        };
        db.Set<MonthlyReport>().Add(report);
        for (var i = 0; i < DefaultColumns.Length; i++)
        {
            var c = DefaultColumns[i];
            db.Set<MonthlyReportColumn>().Add(new MonthlyReportColumn { ReportId = report.Id, ColumnKey = c.Key, Label = c.Label, LabelJa = c.LabelJa, DataType = c.Type, Source = c.Source, SortOrder = i });
        }
        return report;
    }

    private async Task TouchAndSaveAsync(MonthlyReport report, CancellationToken ct)
    {
        report.UpdatedAt = DateTime.UtcNow; // marks the (tracked) report modified so updated_by / updated_at follow every cell edit
        await db.SaveChangesAsync(ct);
    }

    /// <summary>People whose assignment periods overlap the month, minus those removed from the report, by name.</summary>
    private async Task<List<Employee>> MembersAsync(Guid projectId, DateOnly first, DateOnly last, Guid? reportId, CancellationToken ct)
    {
        var ids = await db.Set<ProjectAssignmentPeriod>().AsNoTracking()
            .Where(s => s.Assignment.ProjectId == projectId && !s.Assignment.IsDeleted && s.StartDate <= last && s.EndDate >= first)
            .Select(s => s.Assignment.EmployeeId).Distinct().ToListAsync(ct);
        var removed = reportId is { } rid
            ? await db.Set<MonthlyReportMember>().AsNoTracking().Where(m => m.ReportId == rid && m.IsRemoved).Select(m => m.EmployeeId).ToListAsync(ct)
            : [];
        var vi = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: false);
        return (await db.Set<Employee>().AsNoTracking().Where(e => ids.Contains(e.Id) && !removed.Contains(e.Id)).ToListAsync(ct))
            .OrderBy(e => e.FullName, vi).ToList();
    }

    private async Task<List<MonthlyReportColumnDto>> ColumnsAsync(Guid reportId, CancellationToken ct)
        => (await db.Set<MonthlyReportColumn>().AsNoTracking().Where(c => c.ReportId == reportId).OrderBy(c => c.SortOrder).ToListAsync(ct))
            .Select(c => new MonthlyReportColumnDto(c.ColumnKey, c.Label, c.LabelJa ?? string.Empty, c.DataType, ApiSource(c.Source))).ToList();

    private static List<MonthlyReportColumnDto> DefaultColumnDtos()
        => DefaultColumns.Select(c => new MonthlyReportColumnDto(c.Key, c.Label, c.LabelJa, c.Type, ApiSource(c.Source))).ToList();

    /// <summary>DB column sources check_in / check_out are checkIn / checkOut in the API.</summary>
    private static string? ApiSource(string? source) => source switch { "check_in" => "checkIn", "check_out" => "checkOut", _ => source };

    private static (string Label, string LabelJa, string Type) ParseColumn(ReportColumnRequest r, IReadOnlyCollection<MonthlyReportColumn> columns, string? currentKey)
    {
        var errors = new ValidationErrors();
        var label = Text.Clean(r.Label);
        errors.Required("label", label);
        errors.MaxLength("label", label, 50);
        if (label.Length > 0 && columns.Any(c => c.ColumnKey != currentKey && string.Equals(c.Label, label, StringComparison.OrdinalIgnoreCase)))
            errors.Add("label", "monthly_report_column.err_duplicate");
        var labelJa = Text.Clean(r.LabelJa);
        errors.MaxLength("labelJa", labelJa, 50);
        var type = r.Type ?? "text";
        if (type is not ("text" or "number")) errors.Add("type", "validation.invalid_value");
        errors.ThrowIfAny();
        return (label, labelJa, type);
    }

    private static IReadOnlyDictionary<string, string> Custom(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind != JsonValueKind.Object ? new Dictionary<string, string>()
            : doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText());
    }

    private static string? Hm(TimeOnly? t) => t is { } v ? WorkDates.Hm(v) : null;

    private static string Str(JsonElement? e)
        => e is { ValueKind: JsonValueKind.String } s ? s.GetString()!.Trim() : e is { ValueKind: JsonValueKind.Number } n ? n.GetRawText() : string.Empty;
}
