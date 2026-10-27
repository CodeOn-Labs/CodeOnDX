using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.Organization;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dx.Api.Controllers;

/// <summary>Admin › Departments &amp; teams. Reading is open to every signed-in user (filters, pickers).</summary>
[Route("api/v1")]
[Authorize]
public sealed class DepartmentsController(DepartmentService departments) : ApiControllerBase
{
    [HttpGet("departments")]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<DepartmentDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        try
        {
            return Success(await departments.ListAsync(ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("departments")]
    [HasPermission(Permissions.EmployeeManage)]
    [ProducesResponseType<ErrorFullModel<DepartmentDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDepartment(SaveDepartmentRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await departments.CreateDepartmentAsync(request, ct), "department_create.msg_success", StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("departments/{id:guid}")]
    [HasPermission(Permissions.EmployeeManage)]
    [ProducesResponseType<ErrorFullModel<DepartmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateDepartment(Guid id, SaveDepartmentRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await departments.UpdateDepartmentAsync(id, request, ct), "department_update.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpDelete("departments/{id:guid}")]
    [HasPermission(Permissions.EmployeeManage)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteDepartment(Guid id, CancellationToken ct)
    {
        try
        {
            var name = await departments.DeleteDepartmentAsync(id, ct);
            return Success(null, "department_delete.msg_success", args: new Dictionary<string, object?> { ["name"] = name });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("teams")]
    [HasPermission(Permissions.EmployeeManage)]
    [ProducesResponseType<ErrorFullModel<TeamDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTeam(SaveTeamRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await departments.CreateTeamAsync(request, ct), "team_create.msg_success", StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("teams/{id:guid}")]
    [HasPermission(Permissions.EmployeeManage)]
    [ProducesResponseType<ErrorFullModel<TeamDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTeam(Guid id, SaveTeamRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await departments.UpdateTeamAsync(id, request, ct), "team_update.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpDelete("teams/{id:guid}")]
    [HasPermission(Permissions.EmployeeManage)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteTeam(Guid id, CancellationToken ct)
    {
        try
        {
            var name = await departments.DeleteTeamAsync(id, ct);
            return Success(null, "team_delete.msg_success", args: new Dictionary<string, object?> { ["name"] = name });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }
}
