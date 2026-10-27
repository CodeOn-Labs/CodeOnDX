using System.Text.Json;

namespace Dx.Application.Ai;

/// <summary>
/// Section Integrations:Ai (Q3, D5). Every LLM call goes through the server; the key never
/// reaches the browser. UseMock = true answers from mock-data/ai (insights per screen, chat scenarios).
/// </summary>
public sealed class AiOptions
{
    public const string Section = "Integrations:Ai";

    public bool UseMock { get; set; } = true;

    /// <summary>OpenAI-compatible endpoint, e.g. https://api.openai.com/v1 (POST {BaseUrl}/chat/completions).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int MaxTokens { get; set; } = 2000;

    /// <summary>Secret — appsettings.Secrets.json only.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>How long a generated insight is reused before the LLM is asked again.</summary>
    public int InsightCacheMinutes { get; set; } = 360;
}

/// <summary>Chat-completion call to the configured LLM (only used when UseMock = false).</summary>
public interface IAiClient
{
    Task<string> CompleteAsync(string system, string user, CancellationToken ct);
}

/// <summary>
/// Read-only JSON files under mock-data/ (Q3: JsonMockDataStore) — fictitious data the
/// connectors answer with in mock mode. Paths are relative, e.g. "ai/chat/scenarios.json".
/// </summary>
public interface IMockDataStore
{
    JsonElement? Read(string relativePath);
}

/// <summary>Screens of shared/screens.json (the single screen catalog of FE and BE).</summary>
public interface IScreenCatalog
{
    IReadOnlyList<string> Keys { get; }

    bool Exists(string screenKey);

    /// <summary>The screen, then its parents while the screen inherits insights (ADR-0001).</summary>
    IReadOnlyList<string> InsightChain(string screenKey);
}

/// <summary>Last e-mails "sent" by the mock e-mail connector (Admin › Integrations › preview).</summary>
public interface IEmailOutbox
{
    IReadOnlyList<(string To, string Subject, DateTime SentAt)> Recent(int count);
}
