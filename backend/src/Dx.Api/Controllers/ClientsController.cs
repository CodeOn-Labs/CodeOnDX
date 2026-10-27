using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.Common;
using Dx.Application.Organization;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dx.Api.Controllers;

/// <summary>Admin › Clients. Reading is open to every signed-in user (client filters on other screens).</summary>
[Route("api/v1/clients")]
[Authorize]
public sealed class ClientsController(ClientService clients) : ApiControllerBase
{
    /// <summary>GET /clients?search=&amp;countryIds=&amp;industryIds=&amp;status=active|inactive&amp;deleted=&amp;page=&amp;pageSize=</summary>
    [HttpGet]
    [ProducesResponseType<ErrorFullModel<PagedResult<ClientDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(CancellationToken ct)
    {
        try
        {
            return Success(await clients.SearchAsync(ReadFilter(), QueryParams.Paging(Request.Query), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("summary")]
    [ProducesResponseType<ErrorFullModel<ClientSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        try
        {
            return Success(await clients.SummaryAsync(ReadFilter(), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost]
    [HasPermission(Permissions.AdminSettings)]
    [ProducesResponseType<ErrorFullModel<ClientDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveClientRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await clients.CreateAsync(request, ct), "client_create.msg_success", StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.AdminSettings)]
    [ProducesResponseType<ErrorFullModel<ClientDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, SaveClientRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await clients.UpdateAsync(id, request, ct), "client_update.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>DELETE /clients/{id} — to the trash; a second delete (from the trash) is permanent.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.AdminSettings)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var (permanent, name) = await clients.DeleteAsync(id, ct);
            return Success(null, permanent ? "client_delete.msg_success" : "client_trash.msg_success",
                args: new Dictionary<string, object?> { ["name"] = name });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("{id:guid}/restore")]
    [HasPermission(Permissions.AdminSettings)]
    [ProducesResponseType<ErrorFullModel<ClientDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await clients.RestoreAsync(id, ct);
            return Success(dto, "client_restore.msg_success", args: new Dictionary<string, object?> { ["name"] = dto.Name });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    private ClientFilter ReadFilter()
    {
        var q = Request.Query;
        var errors = new ValidationErrors();
        var search = q["search"].ToString();
        if (search.Length > 200) errors.Add("search", "validation.max_length", new() { ["max"] = 200 });
        var status = q["status"].ToString();
        if (status.Length > 0 && status != "active" && status != "inactive") errors.Add("status", "validation.invalid_value");
        var filter = new ClientFilter(
            search, QueryParams.Guids(q, "countryIds", errors), QueryParams.Guids(q, "industryIds", errors),
            status.Length > 0 ? status : null, string.Equals(q["deleted"], "true", StringComparison.OrdinalIgnoreCase));
        errors.ThrowIfAny();
        return filter;
    }
}
