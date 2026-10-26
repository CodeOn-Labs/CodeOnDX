using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.MasterData;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dx.Api.Controllers;

/// <summary>Lookups, business rules and general settings of the tenant (D12).</summary>
[Route("api/v1")]
[Authorize]
public sealed class ConfigurationController(MasterDataService masterData, RuleService rules, SettingService settings) : ApiControllerBase
{
    /// <summary>GET /master-data?type=project_status,risk_level — active rows, name in the request language.</summary>
    [HttpGet("master-data")]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<MasterDataDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMasterData(CancellationToken ct)
    {
        try
        {
            return Success(await masterData.ListAsync(QueryParams.List(Request.Query, "type"), ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("system-rules")]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<SystemRuleDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRules(CancellationToken ct)
    {
        try
        {
            return Success(await rules.ListAsync(ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("system-rules/{key}")]
    [HasPermission(Permissions.AdminSettings)]
    [ProducesResponseType<ErrorFullModel<SystemRuleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRule(string key, UpdateSystemRuleRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await rules.UpdateAsync(key, request, ct), "system_rule_update.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("settings")]
    [ProducesResponseType<ErrorFullModel<IReadOnlyDictionary<string, string>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        try
        {
            return Success(await settings.GetAllAsync(ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }
}
