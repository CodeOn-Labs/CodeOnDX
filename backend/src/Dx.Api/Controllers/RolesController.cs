using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.Access;
using Dx.Application.Common;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dx.Api.Controllers;

/// <summary>Admin › Roles &amp; permissions (D3) and the user picker. Reading roles / permissions only needs sign-in.</summary>
[Route("api/v1")]
[Authorize]
public sealed class RolesController(RoleService roles) : ApiControllerBase
{
    private static readonly string[] RoleTypes = [RoleService.TypeSystem, RoleService.TypeCustom];

    [HttpGet("permissions")]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<PermissionDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Permissions(CancellationToken ct)
    {
        try
        {
            return Success(await roles.PermissionsAsync(ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>GET /roles?search=&amp;permissions=a,b&amp;types=system,custom&amp;page=&amp;pageSize=</summary>
    [HttpGet("roles")]
    [ProducesResponseType<ErrorFullModel<PagedResult<RoleDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(CancellationToken ct)
    {
        try
        {
            return Success(await roles.SearchAsync(ReadFilter(), QueryParams.Paging(Request.Query), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("roles/summary")]
    [HasPermission(Domain.Constants.Permissions.AdminRoles)]
    [ProducesResponseType<ErrorFullModel<RoleSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        try
        {
            return Success(await roles.SummaryAsync(ReadFilter(), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>PUT /roles/permissions — { updates: [{ roleId, permissions: [...] }] }, all or nothing.</summary>
    [HttpPut("roles/permissions")]
    [HasPermission(Domain.Constants.Permissions.AdminRoles)]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<RoleDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkUpdate(BulkRolePermissionsRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await roles.BulkUpdatePermissionsAsync(request, ct);
            return Success(updated, "role_bulk_update.msg_success", args: new Dictionary<string, object?> { ["count"] = updated.Count });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("roles/{id:guid}")]
    [HasPermission(Domain.Constants.Permissions.AdminRoles)]
    [ProducesResponseType<ErrorFullModel<RoleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Success(await roles.GetAsync(id, ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("roles")]
    [HasPermission(Domain.Constants.Permissions.AdminRoles)]
    [ProducesResponseType<ErrorFullModel<RoleDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveRoleRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await roles.CreateAsync(request, ct), "role_create.msg_success", StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("roles/{id:guid}")]
    [HasPermission(Domain.Constants.Permissions.AdminRoles)]
    [ProducesResponseType<ErrorFullModel<RoleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, SaveRoleRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await roles.UpdateAsync(id, request, ct), "role_update.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpDelete("roles/{id:guid}")]
    [HasPermission(Domain.Constants.Permissions.AdminRoles)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var (name, members) = await roles.DeleteAsync(id, ct);
            return Success(null, "role_delete.msg_success", args: new Dictionary<string, object?> { ["name"] = name, ["count"] = members });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("roles/{id:guid}/users")]
    [HasPermission(Domain.Constants.Permissions.AdminRoles)]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<UserRefDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Members(Guid id, CancellationToken ct)
    {
        try
        {
            return Success(await roles.MembersAsync(id, ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("roles/{id:guid}/users")]
    [HasPermission(Domain.Constants.Permissions.AdminRoles)]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<UserRefDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Assign(Guid id, AssignRoleUserRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await roles.AssignAsync(id, request, ct), "role_assign.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpDelete("roles/{id:guid}/users/{userId:guid}")]
    [HasPermission(Domain.Constants.Permissions.AdminRoles)]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<UserRefDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Unassign(Guid id, Guid userId, CancellationToken ct)
    {
        try
        {
            return Success(await roles.UnassignAsync(id, userId, ct), "role_unassign.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>GET /users?search= — login accounts of the tenant.</summary>
    [HttpGet("users")]
    [HasPermission(Domain.Constants.Permissions.AdminRoles)]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<UserRefDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Users(CancellationToken ct)
    {
        try
        {
            return Success(await roles.UsersAsync(Request.Query["search"].ToString(), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    private RoleFilter ReadFilter()
    {
        var q = Request.Query;
        var errors = new ValidationErrors();
        var search = q["search"].ToString();
        if (search.Length > 200) errors.Add("search", "validation.max_length", new() { ["max"] = 200 });
        var types = QueryParams.List(q, "types");
        if (types.Any(t => !RoleTypes.Contains(t))) errors.Add("types", "validation.invalid_value");
        errors.ThrowIfAny();
        return new RoleFilter(search, QueryParams.List(q, "permissions"), types);
    }
}
