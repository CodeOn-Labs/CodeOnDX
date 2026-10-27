using System.Text.Json;
using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.Common;
using Dx.Application.Work;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dx.Api.Controllers;

/// <summary>Daily work (D4): work log (timesheets), leave &amp; OT requests, check-in / check-out, effort grid.</summary>
[Route("api/v1")]
[Authorize]
public sealed class WorkController(
    TimesheetService timesheets, RequestService requests, AttendanceService attendance, EffortService effort) : ApiControllerBase
{
    private static Dictionary<string, object?> Args(string key, object? value) => new() { [key] = value };

    // ─── timesheets ──────────────────────────────────────────────────────────

    [HttpGet("timesheets/employees")]
    [ProducesResponseType<ErrorFullModel<TimesheetEmployeesDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TimesheetEmployees(CancellationToken ct)
    {
        try
        {
            return Success(await timesheets.EmployeesAsync(ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>GET /timesheets?employeeId=&amp;from=&amp;to= (at most 62 days).</summary>
    [HttpGet("timesheets")]
    [ProducesResponseType<ErrorFullModel<TimesheetDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Timesheet(CancellationToken ct)
    {
        try
        {
            var errors = new ValidationErrors();
            var employeeId = errors.Id("employeeId", Request.Query["employeeId"].ToString());
            errors.ThrowIfAny();
            return Success(await timesheets.GetAsync(employeeId, Request.Query["from"], Request.Query["to"], ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("timesheets")]
    [ProducesResponseType<ErrorFullModel<SaveTimesheetResult>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveTimesheet(SaveTimesheetRequest request, CancellationToken ct)
    {
        try
        {
            var (result, days) = await timesheets.SaveAsync(request, ct);
            return Success(result, "timesheet_save.msg_success", args: Args("days", days));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>GET /timesheets/group?employeeIds=a,b&amp;from=&amp;to=</summary>
    [HttpGet("timesheets/group")]
    [ProducesResponseType<ErrorFullModel<TimesheetGroupDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TimesheetGroup(CancellationToken ct)
    {
        try
        {
            var errors = new ValidationErrors();
            var ids = QueryParams.Guids(Request.Query, "employeeIds", errors);
            errors.ThrowIfAny();
            return Success(await timesheets.GroupAsync(ids, Request.Query["from"], Request.Query["to"], ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("timesheets/approval")]
    [HasPermission(Permissions.TimesheetApprove)]
    [ProducesResponseType<ErrorFullModel<TimesheetApprovalResult>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveTimesheets(TimesheetApprovalRequest request, CancellationToken ct)
    {
        try
        {
            var (result, key, people) = await timesheets.ApproveAsync(request, ct);
            return Success(result, key, args: Args("count", people));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ─── leave ───────────────────────────────────────────────────────────────

    /// <summary>GET /leave-requests?scope=mine|approval&amp;statuses=&amp;leaveTypeIds=&amp;search=&amp;year=&amp;page=&amp;pageSize=</summary>
    [HttpGet("leave-requests")]
    [ProducesResponseType<ErrorFullModel<PagedResult<LeaveRequestDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Leaves(CancellationToken ct)
    {
        try
        {
            return Success(await requests.LeavesAsync(ReadRequestFilter(), QueryParams.Paging(Request.Query), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("leave-requests/summary")]
    [ProducesResponseType<ErrorFullModel<RequestSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> LeaveSummary(CancellationToken ct)
    {
        try
        {
            return Success(await requests.SummaryAsync(ReadYear(), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("leave-requests")]
    [HasPermission(Permissions.TimesheetEdit)]
    [ProducesResponseType<ErrorFullModel<LeaveRequestDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateLeave(CreateLeaveRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await requests.CreateLeaveAsync(request, ct);
            return Success(dto, "leave_create.msg_success", StatusCodes.Status201Created, Args("code", dto.Code));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>POST /leave-requests/{id}/approve | reject | cancel — body { note } (reject needs it).</summary>
    [HttpPost("leave-requests/{id:guid}/{decision:regex(^(approve|reject|cancel)$)}")]
    [ProducesResponseType<ErrorFullModel<LeaveRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DecideLeave(Guid id, string decision, [FromBody] DecideRequest? request, CancellationToken ct)
    {
        try
        {
            var (code, dto) = await requests.DecideLeaveAsync(id, decision, request ?? new DecideRequest(null), ct);
            return Success(dto, $"request_{decision}.msg_success", args: Args("code", code));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ─── overtime ────────────────────────────────────────────────────────────

    [HttpGet("ot-requests")]
    [ProducesResponseType<ErrorFullModel<PagedResult<OtRequestDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Overtimes(CancellationToken ct)
    {
        try
        {
            return Success(await requests.OvertimesAsync(ReadRequestFilter(), QueryParams.Paging(Request.Query), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("ot-requests")]
    [HasPermission(Permissions.TimesheetEdit)]
    [ProducesResponseType<ErrorFullModel<OtRequestDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateOt(CreateOtRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await requests.CreateOtAsync(request, ct);
            return Success(dto, "ot_create.msg_success", StatusCodes.Status201Created, Args("code", dto.Code));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("ot-requests/{id:guid}/{decision:regex(^(approve|reject|cancel)$)}")]
    [ProducesResponseType<ErrorFullModel<OtRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DecideOt(Guid id, string decision, [FromBody] DecideRequest? request, CancellationToken ct)
    {
        try
        {
            var (code, dto) = await requests.DecideOtAsync(id, decision, request ?? new DecideRequest(null), ct);
            return Success(dto, $"request_{decision}.msg_success", args: Args("code", code));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ─── attendance ──────────────────────────────────────────────────────────

    /// <summary>GET /attendance?month=yyyy-MM&amp;search=&amp;departmentIds=&amp;onlyIssues=&amp;page=&amp;pageSize=</summary>
    [HttpGet("attendance")]
    [ProducesResponseType<ErrorFullModel<AttendanceMonthDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Attendance(CancellationToken ct)
    {
        try
        {
            var q = Request.Query;
            var errors = new ValidationErrors();
            var filter = new AttendanceFilter(
                q["month"].ToString() is { Length: > 0 } m ? m : null, q["search"].ToString(),
                QueryParams.Guids(q, "departmentIds", errors), string.Equals(q["onlyIssues"], "true", StringComparison.OrdinalIgnoreCase));
            errors.ThrowIfAny();
            return Success(await attendance.MonthAsync(filter, QueryParams.Paging(q, 50), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("attendance/today")]
    [ProducesResponseType<ErrorFullModel<AttendanceTodayDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AttendanceToday(CancellationToken ct)
    {
        try
        {
            return Success(await attendance.TodayAsync(ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("attendance/check-in")]
    [HasPermission(Permissions.TimesheetEdit)]
    [ProducesResponseType<ErrorFullModel<AttendanceDto>>(StatusCodes.Status200OK)]
    public Task<IActionResult> CheckIn(CancellationToken ct) => CheckAsync(true, ct);

    [HttpPost("attendance/check-out")]
    [HasPermission(Permissions.TimesheetEdit)]
    [ProducesResponseType<ErrorFullModel<AttendanceDto>>(StatusCodes.Status200OK)]
    public Task<IActionResult> CheckOut(CancellationToken ct) => CheckAsync(false, ct);

    private async Task<IActionResult> CheckAsync(bool checkIn, CancellationToken ct)
    {
        try
        {
            var (dto, time) = await attendance.CheckAsync(checkIn, ct);
            return Success(dto, checkIn ? "attendance_check_in.msg_success" : "attendance_check_out.msg_success", args: Args("time", time));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("attendance")]
    [ProducesResponseType<ErrorFullModel<AttendanceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveAttendance(SaveAttendanceRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await attendance.SaveAsync(request, ct), "attendance_save.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("attendance/import")]
    [HasPermission(Permissions.TimesheetAllEdit)]
    [ProducesResponseType<ErrorFullModel<ImportAttendanceResult>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportAttendance(ImportAttendanceRequest request, CancellationToken ct)
    {
        try
        {
            var result = await attendance.ImportAsync(request, ct);
            return Success(result, "attendance_import.msg_success",
                args: new Dictionary<string, object?> { ["imported"] = result.Imported, ["skipped"] = result.Skipped });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ─── effort grid ─────────────────────────────────────────────────────────

    [HttpGet("effort")]
    [HasPermission(Permissions.EffortView)]
    [ProducesResponseType<ErrorFullModel<EffortGridDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Effort(CancellationToken ct)
    {
        try
        {
            return Success(await effort.GridAsync(Request.Query["month"].ToString(), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("effort")]
    [HasPermission(Permissions.EffortEdit)]
    [ProducesResponseType<ErrorFullModel<EffortRowDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddEffort(AddEffortRequest request, CancellationToken ct)
    {
        try
        {
            var (row, name) = await effort.AddAsync(request, ct);
            return Success(row, "effort_add.msg_success", StatusCodes.Status201Created, Args("name", name));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPatch("effort/{id:guid}")]
    [HasPermission(Permissions.EffortEdit)]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<EffortRowDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateEffort(Guid id, [FromBody] Dictionary<string, JsonElement> request, CancellationToken ct)
    {
        try
        {
            var (rows, name) = await effort.UpdateAsync(id, request, ct);
            return Success(rows, "effort_update.msg_success", args: Args("name", name));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpDelete("effort/{id:guid}")]
    [HasPermission(Permissions.EffortEdit)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteEffort(Guid id, CancellationToken ct)
    {
        try
        {
            return Success(null, "effort_delete.msg_success", args: Args("name", await effort.DeleteAsync(id, ct)));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private int? ReadYear()
    {
        var raw = Request.Query["year"].ToString();
        if (raw.Length == 0) return null;
        return int.TryParse(raw, out var y) && y is >= 2000 and <= 2100 ? y : throw AppValidationException.Single("year", "validation.invalid_value");
    }

    private RequestFilter ReadRequestFilter()
    {
        var q = Request.Query;
        var errors = new ValidationErrors();
        var filter = new RequestFilter(
            q["scope"] == RequestService.ScopeApproval ? RequestService.ScopeApproval : RequestService.ScopeMine,
            QueryParams.List(q, "statuses"), QueryParams.Guids(q, "leaveTypeIds", errors), q["search"].ToString(), ReadYear());
        errors.ThrowIfAny();
        return filter;
    }
}
