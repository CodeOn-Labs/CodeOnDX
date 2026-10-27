using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Dx.Api.Controllers;

/// <summary>Admin › PCV criteria (pcv-template-items) and Admin › CSS form (css-template).</summary>
[Route("api/v1")]
[HasPermission(Permissions.AdminSettings)]
public sealed class TemplatesController(TemplateService templates) : ApiControllerBase
{
    // ─── PCV criteria ────────────────────────────────────────────────────────

    [HttpGet("pcv-template-items")]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<PcvTemplateItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPcv(CancellationToken ct)
    {
        try
        {
            return Success(await templates.ListPcvAsync(ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("pcv-template-items")]
    [ProducesResponseType<ErrorFullModel<PcvTemplateItemDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePcv(SavePcvTemplateItemRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await templates.CreatePcvAsync(request, ct), "pcv_template_create.msg_success", StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("pcv-template-items/{id:guid}")]
    [ProducesResponseType<ErrorFullModel<PcvTemplateItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePcv(Guid id, SavePcvTemplateItemRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await templates.UpdatePcvAsync(id, request, ct), "pcv_template_update.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpDelete("pcv-template-items/{id:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeletePcv(Guid id, CancellationToken ct)
    {
        try
        {
            await templates.DeletePcvAsync(id, ct);
            return Success(null, "pcv_template_delete.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    // ─── CSS form ────────────────────────────────────────────────────────────

    [HttpGet("css-template")]
    [ProducesResponseType<ErrorFullModel<CssTemplateDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCss(CancellationToken ct)
    {
        try
        {
            return Success(await templates.GetCssAsync(ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("css-template")]
    [ProducesResponseType<ErrorFullModel<CssTemplateDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCss(SaveCssTemplateRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await templates.UpdateCssAsync(request, ct), "css_template_update.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("css-template/questions")]
    [ProducesResponseType<ErrorFullModel<IReadOnlyList<CssTemplateQuestionDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCssQuestions(CancellationToken ct)
    {
        try
        {
            return Success(await templates.ListCssQuestionsAsync(ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("css-template/questions")]
    [ProducesResponseType<ErrorFullModel<CssTemplateQuestionDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCssQuestion(SaveCssTemplateQuestionRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await templates.CreateCssQuestionAsync(request, ct), "css_question_create.msg_success", StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPut("css-template/questions/{id:guid}")]
    [ProducesResponseType<ErrorFullModel<CssTemplateQuestionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCssQuestion(Guid id, SaveCssTemplateQuestionRequest request, CancellationToken ct)
    {
        try
        {
            return Success(await templates.UpdateCssQuestionAsync(id, request, ct), "css_question_update.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpDelete("css-template/questions/{id:guid}")]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCssQuestion(Guid id, CancellationToken ct)
    {
        try
        {
            await templates.DeleteCssQuestionAsync(id, ct);
            return Success(null, "css_question_delete.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }
}
