using Dx.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dx.Api.Common;

/// <summary>
/// Base of every controller (Q2). Each action is written as
/// <code>try { …; return Success(data, "feature_action.msg_success"); } catch (Exception ex) { return HandleException(ex); }</code>
/// Never returns ex.Message or stack traces.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private JsonStringLocalizer L => HttpContext.RequestServices.GetRequiredService<JsonStringLocalizer>();
    private ILogger Logger => HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

    protected IActionResult Success(object? data = null, string messageKey = "common.msg_success", int status = StatusCodes.Status200OK,
        IReadOnlyDictionary<string, object?>? args = null)
        => StatusCode(status, new ErrorFullModel { Status = status, MessageText = L.Text(messageKey, args), Data = data });

    protected IActionResult HandleException(Exception ex)
    {
        (int Status, string Key, object? Data, Dictionary<string, object?>? Args) r = ex switch
        {
            AppValidationException e => (400, e.MessageKey, LocalizeFieldErrors(e.Errors), e.Args),
            UnauthorizedException e => (401, e.MessageKey, null, e.Args),
            ForbiddenException e => (403, e.MessageKey, null, e.Args),
            NotFoundException e => (404, e.MessageKey, null, e.Args),
            ConflictException e => (409, e.MessageKey, null, e.Args),
            DbUpdateConcurrencyException => (409, "error.concurrency", null, null),
            BusinessException e => (422, e.MessageKey, e.ErrorData, e.Args),
            TooManyRequestsException e => (429, e.MessageKey, null, e.Args),
            OperationCanceledException when HttpContext.RequestAborted.IsCancellationRequested => (499, "error.internal", null, null),
            _ => (500, "error.internal", new { traceId = HttpContext.TraceIdentifier }, null),
        };

        if (r.Status >= 500) Logger.LogError(ex, "Unhandled error {TraceId}", HttpContext.TraceIdentifier);
        else Logger.LogInformation("Handled {Status} {Key} {TraceId}", r.Status, r.Key, HttpContext.TraceIdentifier);

        return StatusCode(r.Status, new ErrorFullModel { Status = r.Status, MessageText = L.Text(r.Key, r.Args), Data = r.Data });
    }

    /// <summary>{ field: [localized message] } with camelCase field names matching the DTO.</summary>
    private Dictionary<string, string[]> LocalizeFieldErrors(IDictionary<string, FieldError[]> errors)
        => errors.ToDictionary(e => e.Key, e => e.Value.Select(f => L.Text(f.Key, f.Args)).ToArray());
}
