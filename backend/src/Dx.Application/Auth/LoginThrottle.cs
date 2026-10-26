using System.Collections.Concurrent;
using Dx.Application.Common;
using Microsoft.Extensions.Options;

namespace Dx.Application.Auth;

/// <summary>
/// In-memory brake on password guessing per client IP: after IpMaxFailures failures within
/// IpWindowMinutes, login answers 429 until the window passes. Single-instance deployment
/// (one Windows Service), so memory is enough. Registered as a singleton.
/// </summary>
public sealed class LoginThrottle(IOptions<AuthOptions> options, TimeProvider time)
{
    private readonly ConcurrentDictionary<string, (int Count, DateTimeOffset Until)> _failures = new();

    public void EnsureAllowed(string ip)
    {
        if (_failures.TryGetValue(ip, out var entry) && entry.Until > time.GetUtcNow() && entry.Count >= options.Value.IpMaxFailures)
            throw new TooManyRequestsException();
    }

    public void RecordFailure(string ip)
    {
        var now = time.GetUtcNow();
        var until = now.AddMinutes(options.Value.IpWindowMinutes);
        _failures.AddOrUpdate(ip, _ => (1, until), (_, e) => e.Until < now ? (1, until) : (e.Count + 1, until));
    }

    public void Reset(string ip) => _failures.TryRemove(ip, out _);
}
