using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Dx.Api.Common;

/// <summary>Writes ErrorFullModel from middleware (the "safety net" for errors outside actions, Q2).</summary>
public static class ErrorResponses
{
    public static Task WriteAsync(HttpContext context, int status, string messageKey, object? data = null)
    {
        var localizer = context.RequestServices.GetRequiredService<JsonStringLocalizer>();
        var json = context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        var body = new ErrorFullModel { Status = status, MessageText = localizer.Text(messageKey), Data = data };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, json));
    }

    public static bool IsApi(HttpContext context) => context.Request.Path.StartsWithSegments("/api");
}
