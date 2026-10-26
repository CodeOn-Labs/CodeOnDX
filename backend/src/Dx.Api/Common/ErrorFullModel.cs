namespace Dx.Api.Common;

/// <summary>
/// The only response shape of the API, success and errors alike (Q2):
/// { "status": 200, "messageText": "…", "data": … } and HTTP status == Status.
/// </summary>
public class ErrorFullModel
{
    public int Status { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public object? Data { get; set; }
}

/// <summary>Same JSON as ErrorFullModel; only used in OpenAPI metadata so orval types `data` precisely.</summary>
public sealed class ErrorFullModel<T>
{
    public int Status { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public T? Data { get; set; }
}
