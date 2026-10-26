using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.Common;
using Dx.Application.Projects;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Dx.Api.Controllers;

[Route("api/v1/projects")]
public sealed class ProjectsController(ProjectService projects) : ApiControllerBase
{
    /// <summary>GET /projects?search=&amp;statusIds=&amp;gradeIds=&amp;clientIds=&amp;riskLevelIds=&amp;startFrom=&amp;endTo=&amp;deleted=&amp;page=&amp;pageSize=</summary>
    [HttpGet]
    [HasPermission(Permissions.ProjectView)]
    [ProducesResponseType<ErrorFullModel<PagedResult<ProjectListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(CancellationToken ct)
    {
        try
        {
            return Success(await projects.SearchAsync(ReadFilter(), QueryParams.Paging(Request.Query), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>GET /projects/summary?&lt;same filter&gt; — KPI cards.</summary>
    [HttpGet("summary")]
    [HasPermission(Permissions.ProjectView)]
    [ProducesResponseType<ErrorFullModel<ProjectSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        try
        {
            return Success(await projects.SummaryAsync(ReadFilter(), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    private ProjectFilter ReadFilter()
    {
        var q = Request.Query;
        var errors = new ValidationErrors();
        var search = q["search"].ToString();
        if (search.Length > 200) errors.Add("search", "validation.max_length", new() { ["max"] = 200 });
        var filter = new ProjectFilter(
            search,
            QueryParams.Guids(q, "statusIds", errors),
            QueryParams.Guids(q, "gradeIds", errors),
            QueryParams.Guids(q, "clientIds", errors),
            QueryParams.Guids(q, "riskLevelIds", errors),
            QueryParams.Date(q, "startFrom", errors),
            QueryParams.Date(q, "endTo", errors),
            string.Equals(q["deleted"], "true", StringComparison.OrdinalIgnoreCase));
        errors.ThrowIfAny();
        return filter;
    }
}
