using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.Common;
using Dx.Application.Organization;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Dx.Api.Controllers;

/// <summary>Admin › Employees (HR records) and invitations to create a login account.</summary>
[Route("api/v1/employees")]
public sealed class EmployeesController(EmployeeService employees) : ApiControllerBase
{
    private static readonly string[] AllocationValues =
    [
        EmployeeService.AllocationNormal, EmployeeService.AllocationLow, EmployeeService.AllocationBench,
        EmployeeService.AllocationOverloaded, EmployeeService.AllocationResigned,
    ];

    /// <summary>
    /// GET /employees?search=&amp;positions=DEV,QA&amp;levelIds=&amp;departmentIds=&amp;teamIds=
    /// &amp;allocation=bench,overloaded&amp;statuses=ACTIVE&amp;deleted=&amp;page=&amp;pageSize=
    /// </summary>
    [HttpGet]
    [HasPermission(Permissions.EmployeeView)]
    [ProducesResponseType<ErrorFullModel<PagedResult<EmployeeDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(CancellationToken ct)
    {
        try
        {
            return Success(await employees.SearchAsync(ReadFilter(), QueryParams.Paging(Request.Query), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("summary")]
    [HasPermission(Permissions.EmployeeView)]
    [ProducesResponseType<ErrorFullModel<EmployeeSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        try
        {
            return Success(await employees.SummaryAsync(ReadFilter(), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.EmployeeView)]
    [ProducesResponseType<ErrorFullModel<EmployeeDetailDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Success(await employees.GetAsync(id, ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost]
    [HasPermission(Permissions.EmployeeManage)]
    [ProducesResponseType<ErrorFullModel<EmployeeDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveEmployeeRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await employees.CreateAsync(request, ct), "employee_create.msg_success", StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.EmployeeManage)]
    [ProducesResponseType<ErrorFullModel<EmployeeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, SaveEmployeeRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await employees.UpdateAsync(id, request, ct), "employee_update.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.EmployeeManage)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var name = await employees.DeleteAsync(id, ct);
            return Success(null, "employee_delete.msg_success", args: new Dictionary<string, object?> { ["name"] = name });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("{id:guid}/restore")]
    [HasPermission(Permissions.EmployeeManage)]
    [ProducesResponseType<ErrorFullModel<EmployeeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await employees.RestoreAsync(id, ct);
            return Success(dto, "employee_restore.msg_success", args: new Dictionary<string, object?> { ["name"] = dto.FullName });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>POST /employees/{id}/invite — e-mails a link to create the login account (D4).</summary>
    [HttpPost("{id:guid}/invite")]
    [HasPermission(Permissions.EmployeeManage)]
    [ProducesResponseType<ErrorFullModel<EmployeeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Invite(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await employees.InviteAsync(id, ct);
            return Success(dto, "employee_invite.msg_success", args: new Dictionary<string, object?> { ["email"] = dto.Email });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    private EmployeeFilter ReadFilter()
    {
        var q = Request.Query;
        var errors = new ValidationErrors();
        var search = q["search"].ToString();
        if (search.Length > 200) errors.Add("search", "validation.max_length", new() { ["max"] = 200 });
        var allocation = QueryParams.List(q, "allocation");
        if (allocation.Any(a => !AllocationValues.Contains(a))) errors.Add("allocation", "validation.invalid_value");
        var filter = new EmployeeFilter(
            search, QueryParams.List(q, "positions"), QueryParams.Guids(q, "levelIds", errors),
            QueryParams.Guids(q, "departmentIds", errors), QueryParams.Guids(q, "teamIds", errors),
            allocation, QueryParams.List(q, "statuses"),
            string.Equals(q["deleted"], "true", StringComparison.OrdinalIgnoreCase));
        errors.ThrowIfAny();
        return filter;
    }
}
