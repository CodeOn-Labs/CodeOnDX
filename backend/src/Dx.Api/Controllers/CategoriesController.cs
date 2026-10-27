using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.Common;
using Dx.Application.MasterData;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Dx.Api.Controllers;

/// <summary>Admin › Categories (tenant-editable lookups).</summary>
[Route("api/v1/categories")]
[HasPermission(Permissions.AdminSettings)]
public sealed class CategoriesController(CategoryService categories) : ApiControllerBase
{
    /// <summary>GET /categories?search=&amp;types=a,b&amp;page=&amp;pageSize=</summary>
    [HttpGet]
    [ProducesResponseType<ErrorFullModel<PagedResult<CategoryDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(CancellationToken ct)
    {
        try
        {
            var search = Request.Query["search"].ToString();
            return Success(await categories.SearchAsync(search, QueryParams.List(Request.Query, "types"), QueryParams.Paging(Request.Query), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost]
    [ProducesResponseType<ErrorFullModel<CategoryDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveCategoryRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await categories.CreateAsync(request, ct), "category_create.msg_success", StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ErrorFullModel<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, SaveCategoryRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await categories.UpdateAsync(id, request, ct), "category_update.msg_success");
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
            var name = await categories.DeleteAsync(id, ct);
            return Success(null, "category_delete.msg_success", args: new Dictionary<string, object?> { ["name"] = name });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }
}
