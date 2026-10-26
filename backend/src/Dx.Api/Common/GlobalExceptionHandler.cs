using Microsoft.AspNetCore.Diagnostics;

namespace Dx.Api.Common;

/// <summary>Safety net for exceptions thrown outside controller actions (middleware, filters) → 500 ErrorFullModel.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled error outside action {TraceId}", context.TraceIdentifier);
        if (context.Response.HasStarted) return false;
        await ErrorResponses.WriteAsync(context, 500, "error.internal", new { traceId = context.TraceIdentifier });
        return true;
    }
}
