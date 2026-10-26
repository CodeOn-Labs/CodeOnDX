using Dx.Application.Auth;
using Dx.Application.Common;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dx.UnitTests;

public sealed class LoginThrottleTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 8, 0, 0, TimeSpan.Zero));
    private readonly LoginThrottle _throttle;

    public LoginThrottleTests()
        => _throttle = new LoginThrottle(Options.Create(new AuthOptions { IpMaxFailures = 5, IpWindowMinutes = 5 }), _time);

    [Fact]
    public void Blocks_after_max_failures_within_window()
    {
        for (var i = 0; i < 4; i++) _throttle.RecordFailure("1.2.3.4");
        Should.NotThrow(() => _throttle.EnsureAllowed("1.2.3.4"));

        _throttle.RecordFailure("1.2.3.4");
        Should.Throw<TooManyRequestsException>(() => _throttle.EnsureAllowed("1.2.3.4"));
        Should.NotThrow(() => _throttle.EnsureAllowed("5.6.7.8"));
    }

    [Fact]
    public void Unblocks_when_window_passes_or_on_success()
    {
        for (var i = 0; i < 5; i++) _throttle.RecordFailure("1.2.3.4");
        _time.Advance(TimeSpan.FromMinutes(6));
        Should.NotThrow(() => _throttle.EnsureAllowed("1.2.3.4"));

        for (var i = 0; i < 5; i++) _throttle.RecordFailure("9.9.9.9");
        _throttle.Reset("9.9.9.9");
        Should.NotThrow(() => _throttle.EnsureAllowed("9.9.9.9"));
    }
}
