using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.Common;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dx.Api.Controllers;

/// <summary>Holidays: every signed-in user reads them (leave / timesheet screens), Admin › Settings edits.</summary>
[Route("api/v1/holidays")]
[Authorize]
public sealed class HolidaysController(HolidayService holidays) : ApiControllerBase
{
    /// <summary>GET /holidays?year=2026</summary>
    [HttpGet]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<HolidayDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        try
        {
            var raw = Request.Query["year"].ToString();
            int? year = null;
            if (raw.Length > 0)
            {
                if (!int.TryParse(raw, out var y) || y is < 1900 or > 9999) throw AppValidationException.Single("year", "validation.invalid_value");
                year = y;
            }
            return Success(await holidays.ListAsync(year, ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost]
    [HasPermission(Permissions.AdminSettings)]
    [ProducesResponseType<ErrorFullModel<HolidayDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateHolidayRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await holidays.CreateAsync(request, ct), "holiday_create.msg_success", StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.AdminSettings)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await holidays.DeleteAsync(id, ct);
            return Success(null, "holiday_delete.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }
}
