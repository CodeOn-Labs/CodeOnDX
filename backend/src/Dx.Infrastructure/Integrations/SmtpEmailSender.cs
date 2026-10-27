using System.Net;
using System.Net.Mail;
using Dx.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dx.Infrastructure.Integrations;

/// <summary>
/// Email connector sending through SMTP (Integrations:Email:UseMock = false). Plain-text mails
/// rendered from the API locales; failures are logged and rethrown so the caller answers 500.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options, IMessageLocalizer localizer, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var o = options.Value;
        var (subject, body) = EmailTemplates.Render(localizer, message);
        using var mail = new MailMessage(new MailAddress(o.FromAddress, o.FromName), new MailAddress(message.To))
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        using var client = new SmtpClient(o.SmtpHost, o.SmtpPort) { EnableSsl = o.EnableSsl };
        if (o.SmtpUser.Length > 0) client.Credentials = new NetworkCredential(o.SmtpUser, o.SmtpPassword);
        try
        {
            await client.SendMailAsync(mail, ct);
        }
        catch (SmtpException ex)
        {
            logger.LogError(ex, "Sending e-mail {Template} failed", message.Template);
            throw;
        }
    }
}
