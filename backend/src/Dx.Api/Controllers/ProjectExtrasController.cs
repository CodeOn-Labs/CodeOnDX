using System.Text.Json;
using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.Common;
using Dx.Application.Projects;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Dx.Api.Controllers;

/// <summary>
/// Project detail sub-tabs: Metrics › PCV / CSS, Project admin (documents, risks, changes,
/// issues), Monthly report, Status report. Every action needs project.view; the services check
/// membership and the write permission (pcv.manage, css.manage, project.manage).
/// </summary>
[Route("api/v1/projects/{id:guid}")]
[HasPermission(Permissions.ProjectView)]
public sealed class ProjectExtrasController(
    ProjectQualityFormsService forms, ProjectAdminService admin, MonthlyReportService reports) : ApiControllerBase
{
    private const string Month = "{month:regex(^\\d{{4}}-\\d{{2}}$)}";
    private const string Segment = "{segment:regex(^(document-groups|documents|risks|changes|issues)$)}";

    private static Dictionary<string, object?> Args(string key, object? value) => new() { [key] = value };

    // ─── PCV ─────────────────────────────────────────────────────────────────

    [HttpGet("pcv")]
    [ProducesResponseType<ErrorFullModel<PcvDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Pcv(Guid id, CancellationToken ct)
    {
        try { return Success(await forms.PcvAsync(id, Request.Query["month"].ToString(), ct)); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("pcv")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SavePcv(Guid id, SavePcvRequest request, CancellationToken ct)
    {
        try
        {
            var (month, score) = await forms.SavePcvAsync(id, request, ct);
            return Success(null, "project_pcv_save.msg_success",
                args: new Dictionary<string, object?> { ["month"] = month, ["score"] = score.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) });
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("pcv/items")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePcvItem(Guid id, SavePcvItemRequest request, CancellationToken ct)
    {
        try
        {
            await forms.CreatePcvItemAsync(id, request, ct);
            return Success(null, "project_pcv_item_create.msg_success", StatusCodes.Status201Created);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("pcv/items/{itemId:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePcvItem(Guid id, Guid itemId, SavePcvItemRequest request, CancellationToken ct)
    {
        try
        {
            await forms.UpdatePcvItemAsync(id, itemId, request, ct);
            return Success(null, "project_pcv_item_update.msg_success");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete("pcv/items/{itemId:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeletePcvItem(Guid id, Guid itemId, CancellationToken ct)
    {
        try
        {
            await forms.DeletePcvItemAsync(id, itemId, ct);
            return Success(null, "project_pcv_item_delete.msg_success");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // ─── CSS ─────────────────────────────────────────────────────────────────

    [HttpGet("css")]
    [ProducesResponseType<ErrorFullModel<ProjectCssDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Css(Guid id, CancellationToken ct)
    {
        try { return Success(await forms.CssAsync(id, Request.Query["month"].ToString(), ct)); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("css/form")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCssForm(Guid id, SaveCssFormRequest request, CancellationToken ct)
    {
        try
        {
            await forms.UpdateCssFormAsync(id, request, ct);
            return Success(null, "project_css_form_update.msg_success");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("css/questions")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCssQuestion(Guid id, SaveCssQuestionRequest request, CancellationToken ct)
    {
        try
        {
            await forms.CreateCssQuestionAsync(id, request, ct);
            return Success(null, "project_css_question_create.msg_success", StatusCodes.Status201Created);
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("css/questions/{questionId:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCssQuestion(Guid id, Guid questionId, SaveCssQuestionRequest request, CancellationToken ct)
    {
        try
        {
            await forms.UpdateCssQuestionAsync(id, questionId, request, ct);
            return Success(null, "project_css_question_update.msg_success");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete("css/questions/{questionId:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCssQuestion(Guid id, Guid questionId, CancellationToken ct)
    {
        try
        {
            await forms.DeleteCssQuestionAsync(id, questionId, ct);
            return Success(null, "project_css_question_delete.msg_success");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    /// <summary>POST /projects/{id}/css/link — { month } → { token } of a new public survey link.</summary>
    [HttpPost("css/link")]
    [ProducesResponseType<ErrorFullModel<CssLinkDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCssLink(Guid id, CssLinkRequest request, CancellationToken ct)
    {
        try
        {
            var (link, month) = await forms.CreateLinkAsync(id, request, ct);
            return Success(link, "project_css_link.msg_success", StatusCodes.Status201Created, Args("month", month));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // ─── project admin ───────────────────────────────────────────────────────

    /// <summary>GET documents | document-groups | risks | changes | issues of the project.</summary>
    [HttpGet(Segment)]
    public async Task<IActionResult> AdminList(Guid id, string segment, CancellationToken ct)
    {
        try { return Success(await admin.ListAsync(id, segment, ct)); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost(Segment)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AdminCreate(Guid id, string segment, [FromBody] JsonElement request, CancellationToken ct)
    {
        try
        {
            var (key, label) = await admin.CreateAsync(id, segment, request, ct);
            return Success(null, $"{key}_create.msg_success", StatusCodes.Status201Created, Args("name", label));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut(Segment + "/{itemId:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminUpdate(Guid id, string segment, Guid itemId, [FromBody] JsonElement request, CancellationToken ct)
    {
        try
        {
            var (key, label) = await admin.UpdateAsync(id, segment, itemId, request, ct);
            return Success(null, $"{key}_update.msg_success", args: Args("name", label));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete(Segment + "/{itemId:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminDelete(Guid id, string segment, Guid itemId, CancellationToken ct)
    {
        try
        {
            var (key, label) = await admin.DeleteAsync(id, segment, itemId, ct);
            return Success(null, $"{key}_delete.msg_success", args: Args("name", label));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    // ─── monthly report ──────────────────────────────────────────────────────

    [HttpGet("monthly-reports/" + Month)]
    [ProducesResponseType<ErrorFullModel<MonthlyReportDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MonthlyReport(Guid id, string month, CancellationToken ct)
    {
        try { return Success(await reports.GetAsync(id, ValidMonth(month), ct)); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPatch("monthly-reports/" + Month + "/header")]
    [ProducesResponseType<ErrorFullModel<MonthlyReportSavedDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MonthlyReportHeader(Guid id, string month, ReportHeaderRequest request, CancellationToken ct)
    {
        try { return Success(await reports.UpdateHeaderAsync(id, ValidMonth(month), request, ct), "monthly_report_save.msg_success"); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPatch("monthly-reports/" + Month + "/cells")]
    [ProducesResponseType<ErrorFullModel<MonthlyReportSavedDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MonthlyReportCell(Guid id, string month, ReportCellRequest request, CancellationToken ct)
    {
        try { return Success(await reports.UpdateCellAsync(id, ValidMonth(month), request, ct), "monthly_report_save.msg_success"); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("monthly-reports/" + Month + "/sync")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MonthlyReportSync(Guid id, string month, ReportSyncRequest request, CancellationToken ct)
    {
        try
        {
            var (kind, count) = await reports.SyncAsync(id, ValidMonth(month), request, ct);
            return Success(null, kind == "time" ? "monthly_report_sync_time.msg_success" : "monthly_report_sync_hours.msg_success", args: Args("count", count));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("monthly-reports/" + Month + "/columns")]
    [ProducesResponseType<ErrorFullModel<MonthlyReportColumnCreatedDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateReportColumn(Guid id, string month, ReportColumnRequest request, CancellationToken ct)
    {
        try { return Success(await reports.CreateColumnAsync(id, ValidMonth(month), request, ct), "monthly_report_column_create.msg_success", StatusCodes.Status201Created); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("monthly-reports/" + Month + "/columns/{columnKey}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateReportColumn(Guid id, string month, string columnKey, ReportColumnRequest request, CancellationToken ct)
    {
        try
        {
            await reports.UpdateColumnAsync(id, ValidMonth(month), columnKey, request, ct);
            return Success(null, "monthly_report_column_update.msg_success");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete("monthly-reports/" + Month + "/columns/{columnKey}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteReportColumn(Guid id, string month, string columnKey, CancellationToken ct)
    {
        try { return Success(null, "monthly_report_column_delete.msg_success", args: Args("name", await reports.DeleteColumnAsync(id, ValidMonth(month), columnKey, ct))); }
        catch (Exception ex) { return HandleException(ex); }
    }

    // ─── status report ───────────────────────────────────────────────────────

    [HttpGet("status-report")]
    [ProducesResponseType<ErrorFullModel<StatusReportDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> StatusReport(Guid id, CancellationToken ct)
    {
        try { return Success(await reports.StatusReportAsync(id, ct)); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("status-report")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveStatusReport(Guid id, SaveStatusReportRequest request, CancellationToken ct)
    {
        try
        {
            await reports.SaveStatusReportAsync(id, request, ct);
            return Success(null, "project_status_report_save.msg_success");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("status-report/ai-fill")]
    [ProducesResponseType<ErrorFullModel<StatusReportFillDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FillStatusReport(Guid id, CancellationToken ct)
    {
        try { return Success(await reports.AiFillAsync(id, ct), "project_status_report_ai.msg_success"); }
        catch (Exception ex) { return HandleException(ex); }
    }

    private static string ValidMonth(string month)
        => ProjectTimelineService.IsMonth(month) ? month : throw AppValidationException.Single("month", "validation.invalid_value");
}
