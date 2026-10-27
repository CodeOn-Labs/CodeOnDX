namespace Dx.Application.Common;

/// <summary>
/// Business exceptions (Q2). Thrown by services, never caught there; the controller's
/// HandleException maps them to ErrorFullModel with the matching HTTP status.
/// MessageKey is a key in backend/src/Dx.Api/Locales/*.json; Args fill {snake_case} placeholders.
/// </summary>
public abstract class AppException(string messageKey, object? data = null, Dictionary<string, object?>? args = null)
    : Exception(messageKey)
{
    public string MessageKey { get; } = messageKey;
    public object? ErrorData { get; } = data;
    public Dictionary<string, object?> Args { get; } = args ?? new();
}

/// <summary>400 — field errors. Values are locale keys (optionally with args) resolved by the API.</summary>
public sealed class AppValidationException(IDictionary<string, FieldError[]> errors)
    : AppException("error.validation_failed", errors)
{
    public IDictionary<string, FieldError[]> Errors { get; } = errors;

    public static AppValidationException Single(string field, string key, Dictionary<string, object?>? args = null)
        => new(new Dictionary<string, FieldError[]> { [field] = [new FieldError(key, args)] });
}

public sealed record FieldError(string Key, Dictionary<string, object?>? Args = null);

public sealed class UnauthorizedException(string key = "error.unauthorized") : AppException(key);
public sealed class NotFoundException(string key = "error.not_found") : AppException(key);
public sealed class ForbiddenException(string key = "error.forbidden") : AppException(key);
public sealed class ConflictException(string key = "error.conflict", Dictionary<string, object?>? args = null) : AppException(key, null, args);

/// <summary>410 — a one-time link that expired or was already used.</summary>
public sealed class GoneException(string key) : AppException(key);

/// <summary>502 — an external service (LLM, connector) failed; details only go to the log.</summary>
public sealed class UpstreamException(string key) : AppException(key);
public sealed class TooManyRequestsException(string key = "error.too_many_requests") : AppException(key);

/// <summary>422 — business rule violated, e.g. "timesheet_week_save.err_week_locked".</summary>
public sealed class BusinessException(string key, object? data = null, Dictionary<string, object?>? args = null)
    : AppException(key, data, args);

/// <summary>Collects field errors and throws one AppValidationException.</summary>
public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<FieldError>> _errors = new();

    public bool Any => _errors.Count > 0;

    public ValidationErrors Add(string field, string key, Dictionary<string, object?>? args = null)
    {
        if (!_errors.TryGetValue(field, out var list)) _errors[field] = list = [];
        list.Add(new FieldError(key, args));
        return this;
    }

    public void ThrowIfAny()
    {
        if (Any) throw new AppValidationException(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
    }
}
