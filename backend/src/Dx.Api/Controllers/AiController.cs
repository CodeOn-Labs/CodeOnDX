using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.Ai;
using Dx.Application.Common;
using Dx.Application.Projects;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dx.Api.Controllers;

/// <summary>AI Insight panel, Chat AI (D5), Admin › AI Insight / Prompt templates / Integrations (Q3).</summary>
[Route("api/v1")]
[Authorize]
public sealed class AiController(AiService ai, AiAdminService admin) : ApiControllerBase
{
    /// <summary>GET /ai/insights?screen=project_list&amp;refresh=true</summary>
    [HttpGet("ai/insights")]
    [ProducesResponseType<ErrorFullModel<InsightsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Insights(CancellationToken ct)
    {
        try
        {
            var refresh = string.Equals(Request.Query["refresh"], "true", StringComparison.OrdinalIgnoreCase);
            return Success(await ai.InsightsAsync(Request.Query["screen"].ToString(), refresh, ct), "ai_insight_run.msg_success");
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("ai/chat/greeting")]
    [ProducesResponseType<ErrorFullModel<ChatGreetingDto>>(StatusCodes.Status200OK)]
    public IActionResult Greeting()
    {
        try { return Success(ai.Greeting()); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("ai/chat")]
    [ProducesResponseType<ErrorFullModel<ChatReplyDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Chat(ChatRequest request, CancellationToken ct)
    {
        try { return Success(await ai.ChatAsync(request, ct), "ai_chat_send.msg_success"); }
        catch (Exception ex) { return HandleException(ex); }
    }

    // ─── insight configs ─────────────────────────────────────────────────────

    /// <summary>GET /ai/insight-configs?search=&amp;screens=&amp;statuses=active,paused&amp;page=&amp;pageSize=</summary>
    [HttpGet("ai/insight-configs")]
    [HasPermission(Permissions.AdminInsights)]
    [ProducesResponseType<ErrorFullModel<PagedResult<InsightConfigDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> InsightConfigs(CancellationToken ct)
    {
        try
        {
            var q = Request.Query;
            return Success(await admin.InsightConfigsAsync(q["search"], QueryParams.List(q, "screens"), QueryParams.List(q, "statuses"), QueryParams.Paging(q), ct));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("ai/insight-configs/preview")]
    [HasPermission(Permissions.AdminInsights)]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<InsightItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewInsight(InsightPreviewRequest request, CancellationToken ct)
    {
        try { return Success(await admin.PreviewInsightAsync(request, ct), "ai_insight_run.msg_success"); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("ai/insight-configs/{id:guid}")]
    [HasPermission(Permissions.AdminInsights)]
    [ProducesResponseType<ErrorFullModel<InsightConfigDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> InsightConfig(Guid id, CancellationToken ct)
    {
        try { return Success(await admin.InsightConfigAsync(id, ct)); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("ai/insight-configs")]
    [HasPermission(Permissions.AdminInsights)]
    [ProducesResponseType<ErrorFullModel<InsightConfigDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInsightConfig(SaveInsightConfigRequest request, CancellationToken ct)
    {
        try { return Success(await admin.CreateInsightConfigAsync(request, ct), "ai_insight_create.msg_success", StatusCodes.Status201Created); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("ai/insight-configs/{id:guid}")]
    [HasPermission(Permissions.AdminInsights)]
    [ProducesResponseType<ErrorFullModel<InsightConfigDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateInsightConfig(Guid id, SaveInsightConfigRequest request, CancellationToken ct)
    {
        try { return Success(await admin.UpdateInsightConfigAsync(id, request, ct), "ai_insight_update.msg_success"); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete("ai/insight-configs/{id:guid}")]
    [HasPermission(Permissions.AdminInsights)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteInsightConfig(Guid id, CancellationToken ct)
    {
        try { return Success(null, "ai_insight_delete.msg_success", args: new Dictionary<string, object?> { ["name"] = await admin.DeleteInsightConfigAsync(id, ct) }); }
        catch (Exception ex) { return HandleException(ex); }
    }

    // ─── prompt templates ────────────────────────────────────────────────────

    /// <summary>GET /ai/prompt-templates?search=&amp;scopes=&amp;statuses=&amp;page=&amp;pageSize=</summary>
    [HttpGet("ai/prompt-templates")]
    [HasPermission(Permissions.AdminInsights)]
    [ProducesResponseType<ErrorFullModel<PagedResult<PromptTemplateDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PromptTemplates(CancellationToken ct)
    {
        try
        {
            var q = Request.Query;
            return Success(await admin.PromptTemplatesAsync(q["search"], QueryParams.List(q, "scopes"), QueryParams.List(q, "statuses"), QueryParams.Paging(q), ct));
        }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("ai/prompt-templates/preview")]
    [HasPermission(Permissions.AdminInsights)]
    [ProducesResponseType<ErrorFullModel<PromptPreviewDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewPrompt(PromptPreviewRequest request, CancellationToken ct)
    {
        try { return Success(await admin.PreviewPromptAsync(request, ct)); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("ai/prompt-templates/{id:guid}")]
    [HasPermission(Permissions.AdminInsights)]
    [ProducesResponseType<ErrorFullModel<PromptTemplateDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PromptTemplate(Guid id, CancellationToken ct)
    {
        try { return Success(await admin.PromptTemplateAsync(id, ct)); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("ai/prompt-templates")]
    [HasPermission(Permissions.AdminInsights)]
    [ProducesResponseType<ErrorFullModel<PromptTemplateDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePromptTemplate(SavePromptTemplateRequest request, CancellationToken ct)
    {
        try { return Success(await admin.CreatePromptTemplateAsync(request, ct), "prompt_template_create.msg_success", StatusCodes.Status201Created); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("ai/prompt-templates/{id:guid}")]
    [HasPermission(Permissions.AdminInsights)]
    [ProducesResponseType<ErrorFullModel<PromptTemplateDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePromptTemplate(Guid id, SavePromptTemplateRequest request, CancellationToken ct)
    {
        try { return Success(await admin.UpdatePromptTemplateAsync(id, request, ct), "prompt_template_update.msg_success"); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete("ai/prompt-templates/{id:guid}")]
    [HasPermission(Permissions.AdminInsights)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeletePromptTemplate(Guid id, CancellationToken ct)
    {
        try { return Success(null, "prompt_template_delete.msg_success", args: new Dictionary<string, object?> { ["name"] = await admin.DeletePromptTemplateAsync(id, ct) }); }
        catch (Exception ex) { return HandleException(ex); }
    }

    // ─── integrations ────────────────────────────────────────────────────────

    [HttpGet("integrations")]
    [HasPermission(Permissions.AdminIntegrations)]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<IntegrationDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Integrations(CancellationToken ct)
    {
        try { return Success(await admin.IntegrationsAsync(ct)); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPut("integrations/{key:regex(^[[A-Za-z]]+$)}")]
    [HasPermission(Permissions.AdminIntegrations)]
    [ProducesResponseType<ErrorFullModel<IntegrationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateIntegration(string key, SaveIntegrationRequest request, CancellationToken ct)
    {
        try { return Success(await admin.UpdateIntegrationAsync(key, request, ct), "integration_update.msg_success"); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("integrations/{key:regex(^[[A-Za-z]]+$)}/test")]
    [HasPermission(Permissions.AdminIntegrations)]
    [ProducesResponseType<ErrorFullModel<IntegrationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TestIntegration(string key, CancellationToken ct)
    {
        try { return Success(await admin.TestIntegrationAsync(key, ct), "integration_test.msg_success"); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("integrations/{key:regex(^[[A-Za-z]]+$)}/preview")]
    [HasPermission(Permissions.AdminIntegrations)]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<IntegrationPreviewItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewIntegration(string key, CancellationToken ct)
    {
        try { return Success(await admin.PreviewIntegrationAsync(key, ct)); }
        catch (Exception ex) { return HandleException(ex); }
    }
}

/// <summary>Public CSS survey page: the customer opens the link without an account (tenant found from the token).</summary>
[Route("api/v1/public/css-surveys")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class PublicSurveyController(ProjectQualityFormsService forms) : ApiControllerBase
{
    [HttpGet("{token}")]
    [ProducesResponseType<ErrorFullModel<PublicCssSurveyDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Survey(string token, CancellationToken ct)
    {
        try { return Success(await forms.PublicSurveyAsync(token, ct)); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpPost("{token}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(string token, SubmitCssSurveyRequest request, CancellationToken ct)
    {
        try
        {
            await forms.SubmitSurveyAsync(token, request, ct);
            return Success(null, "css_survey_submit.msg_success");
        }
        catch (Exception ex) { return HandleException(ex); }
    }
}
