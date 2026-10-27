using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.Common;
using Dx.Application.Projects;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Dx.Api.Controllers;

/// <summary>
/// One project: detail, create / edit / trash, import, staffing, timeline, EE and quality.
/// Every action needs project.view; the services add the per-project checks (assigned member, or
/// project.manage to change anything).
/// </summary>
[Route("api/v1/projects")]
[HasPermission(Permissions.ProjectView)]
public sealed class ProjectDetailController(
    ProjectDetailService details, ProjectStaffingService staffing, ProjectTimelineService timeline) : ApiControllerBase
{
    private static Dictionary<string, object?> Name(string name) => new() { ["name"] = name };

    [HttpPost]
    [HasPermission(Permissions.ProjectCreate)]
    [ProducesResponseType<ErrorFullModel<ProjectDetailDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveProjectRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await details.CreateAsync(request, ct);
            return Success(dto, "project_create.msg_success", StatusCodes.Status201Created, Name(dto.Name));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>POST /projects/import — { rows: [{ row, values: { name, client, … } }] }; answers created count + errors per row.</summary>
    [HttpPost("import")]
    [HasPermission(Permissions.ProjectCreate)]
    [ProducesResponseType<ErrorFullModel<ImportProjectsResult>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(ImportProjectsRequest request, CancellationToken ct)
    {
        try
        {
            var result = await details.ImportAsync(request, ct);
            return Success(result, "project_import.msg_success", args: new Dictionary<string, object?> { ["count"] = result.Created });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ErrorFullModel<ProjectDetailDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Success(await details.GetAsync(id, ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ErrorFullModel<ProjectDetailDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, SaveProjectRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await details.UpdateAsync(id, request, ct);
            return Success(dto, "project_update.msg_success", args: Name(dto.Name));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            return Success(null, "project_delete.msg_success", args: Name(await details.DeleteAsync(id, ct)));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("{id:guid}/restore")]
    [HasPermission(Permissions.ProjectManage)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        try
        {
            return Success(null, "project_restore.msg_success", args: Name(await details.RestoreAsync(id, ct)));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ─── staffing ────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/staffing")]
    [ProducesResponseType<ErrorFullModel<StaffingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Staffing(Guid id, CancellationToken ct)
    {
        try
        {
            return Success(await staffing.StaffingAsync(id, ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("{id:guid}/assignable-employees")]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<AssignableEmployeeDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Assignable(Guid id, CancellationToken ct)
    {
        try
        {
            return Success(await staffing.AssignableAsync(id, ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("{id:guid}/assignments")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Assign(Guid id, SaveAssignmentRequest request, CancellationToken ct)
    {
        try
        {
            var name = await staffing.CreateAsync(id, request, ct);
            return Success(null, "project_assignment_create.msg_success", StatusCodes.Status201Created, Name(name));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("{id:guid}/assignments/{assignmentId:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAssignment(Guid id, Guid assignmentId, SaveAssignmentRequest request, CancellationToken ct)
    {
        try
        {
            var (key, name) = await staffing.UpdateAsync(id, assignmentId, request, ct);
            return Success(null, key, args: Name(name));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>DELETE /projects/{id}/assignments/{assignmentId}?intervalId= — one period or the whole assignment.</summary>
    [HttpDelete("{id:guid}/assignments/{assignmentId:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAssignment(Guid id, Guid assignmentId, CancellationToken ct)
    {
        try
        {
            var errors = new ValidationErrors();
            var intervalId = errors.Id("intervalId", Request.Query["intervalId"].ToString());
            errors.ThrowIfAny();
            return Success(null, "project_assignment_delete.msg_success", args: Name(await staffing.DeleteAsync(id, assignmentId, intervalId, ct)));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ─── timeline ────────────────────────────────────────────────────────────

    /// <summary>GET /projects/{id}/milestones?from=&amp;to= (timeliness range).</summary>
    [HttpGet("{id:guid}/milestones")]
    [ProducesResponseType<ErrorFullModel<MilestonesDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Milestones(Guid id, CancellationToken ct)
    {
        try
        {
            var errors = new ValidationErrors();
            var from = QueryParams.Date(Request.Query, "from", errors);
            var to = QueryParams.Date(Request.Query, "to", errors);
            errors.ThrowIfAny();
            return Success(await timeline.MilestonesAsync(id, from, to, ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPatch("{id:guid}/dates")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateDates(Guid id, ProjectDatesRequest request, CancellationToken ct)
    {
        try
        {
            await timeline.UpdateDatesAsync(id, request, ct);
            return Success(null, "project_dates_update.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("{id:guid}/milestones")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMilestone(Guid id, SaveMilestoneRequest request, CancellationToken ct)
    {
        try
        {
            var name = await timeline.CreateMilestoneAsync(id, request, ct);
            return Success(null, "project_milestone_create.msg_success", StatusCodes.Status201Created, Name(name));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("{id:guid}/milestones/{milestoneId:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMilestone(Guid id, Guid milestoneId, SaveMilestoneRequest request, CancellationToken ct)
    {
        try
        {
            return Success(null, "project_milestone_update.msg_success", args: Name(await timeline.UpdateMilestoneAsync(id, milestoneId, request, ct)));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpDelete("{id:guid}/milestones/{milestoneId:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteMilestone(Guid id, Guid milestoneId, CancellationToken ct)
    {
        try
        {
            return Success(null, "project_milestone_delete.msg_success", args: Name(await timeline.DeleteMilestoneAsync(id, milestoneId, ct)));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ─── metrics ─────────────────────────────────────────────────────────────

    /// <summary>GET /projects/{id}/ee?month=yyyy-MM (month of the member chart).</summary>
    [HttpGet("{id:guid}/ee")]
    [ProducesResponseType<ErrorFullModel<EeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Ee(Guid id, CancellationToken ct)
    {
        try
        {
            var month = Request.Query["month"].ToString();
            if (month.Length > 0 && !ProjectTimelineService.IsMonth(month)) throw AppValidationException.Single("month", "validation.invalid_value");
            return Success(await timeline.EeAsync(id, month.Length > 0 ? month : null, ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>GET /projects/{id}/quality?kind=defect|leakage&amp;year=</summary>
    [HttpGet("{id:guid}/quality")]
    [ProducesResponseType<ErrorFullModel<QualityDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Quality(Guid id, CancellationToken ct)
    {
        try
        {
            return Success(await timeline.QualityAsync(id, Request.Query["kind"].ToString(), Request.Query["year"].ToString(), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("{id:guid}/quality")]
    [ProducesResponseType<ErrorFullModel<QualityDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveQuality(Guid id, SaveQualityRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await timeline.SaveQualityAsync(id, request, ct), "project_quality_save.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }
}
