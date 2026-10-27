using System.Collections.Concurrent;
using Dx.Application.Ai;
using Dx.Application.Common;
using Microsoft.Extensions.Logging;

namespace Dx.Infrastructure.Integrations;

/// <summary>
/// Email connector in mock mode (Integrations:Email:UseMock = true): the rendered message is
/// written to the log instead of being sent, so invitation / reset links can be followed on a dev
/// machine. The last 50 messages stay in memory for Admin › Integrations › Email › preview.
/// </summary>
public sealed class MockEmailSender(IMessageLocalizer localizer, ILogger<MockEmailSender> logger) : IEmailSender, IEmailOutbox
{
    private readonly ConcurrentQueue<(string To, string Subject, DateTime SentAt)> _outbox = new();

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var (subject, body) = EmailTemplates.Render(localizer, message);
        logger.LogInformation("[mock e-mail] to {To}: {Subject}\n{Body}", message.To, subject, body);
        _outbox.Enqueue((message.To, subject, DateTime.UtcNow));
        while (_outbox.Count > 50) _outbox.TryDequeue(out _);
        return Task.CompletedTask;
    }

    public IReadOnlyList<(string To, string Subject, DateTime SentAt)> Recent(int count) => _outbox.Reverse().Take(count).ToList();
}

/// <summary>The SMTP sender keeps no outbox.</summary>
public sealed class NoEmailOutbox : IEmailOutbox
{
    public IReadOnlyList<(string To, string Subject, DateTime SentAt)> Recent(int count) => [];
}

internal static class EmailTemplates
{
    public static (string Subject, string Body) Render(IMessageLocalizer localizer, EmailMessage message)
    {
        var args = message.Args.ToDictionary(p => p.Key, p => (object?)p.Value);
        return (localizer.Text($"email_{message.Template}.subject", args, message.Culture),
                localizer.Text($"email_{message.Template}.body", args, message.Culture));
    }
}
