namespace Dx.Application.Common;

/// <summary>
/// One outgoing e-mail. <see cref="Template"/> names the mail ("invitation", "password_reset");
/// subject and body are the API locale keys email_&lt;template&gt;.subject / .body in
/// <see cref="Culture"/>, with {placeholders} filled from <see cref="Args"/> (link, names…).
/// </summary>
public sealed record EmailMessage(string To, string Template, IReadOnlyDictionary<string, string> Args, string Culture);

/// <summary>Email connector (Q3, section Integrations:Email).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>API messages (backend/src/Dx.Api/Locales) for code outside a request, e.g. e-mail templates.</summary>
public interface IMessageLocalizer
{
    string Text(string key, IReadOnlyDictionary<string, object?>? args = null, string? culture = null);
}

/// <summary>
/// Section Integrations:Email. Non-secret values live in appsettings.json; SmtpPassword only in
/// appsettings.Secrets.json.
/// </summary>
public sealed class EmailOptions
{
    public const string Section = "Integrations:Email";

    /// <summary>true = do not send, only log the message (dev machine, public demo with fictitious data).</summary>
    public bool UseMock { get; set; } = true;

    /// <summary>Base URL of the web app used in links, e.g. https://dx.codeonlabs.vn.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5173";

    public string FromAddress { get; set; } = "no-reply@example.com";
    public string FromName { get; set; } = "DX Platform";
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;

    /// <summary>STARTTLS / TLS (System.Net.Mail.SmtpClient.EnableSsl).</summary>
    public bool EnableSsl { get; set; } = true;
}
