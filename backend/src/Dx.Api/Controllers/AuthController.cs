using Dx.Api.Common;
using Dx.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Dx.Api.Controllers;

/// <summary>
/// Sign-in endpoints (D2). Access token in the body; refresh token in cookie dx_rt
/// (HttpOnly, SameSite=Strict, Secure outside Development, Path=/api/v1/auth) — never in localStorage.
/// </summary>
[Route("api/v1/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(AuthService auth, IOptions<AuthOptions> options, IWebHostEnvironment env) : ApiControllerBase
{
    public const string RefreshCookie = "dx_rt";

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<ErrorFullModel<AuthResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        try
        {
            var session = await auth.LoginAsync(request, ClientIp, UserAgent, ct);
            SetRefreshCookie(session.RefreshToken, session.RefreshExpiresAt);
            return Success(session.Response, "auth_login.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Refresh)] // every page load refreshes once: own, looser bucket (S4)
    [ProducesResponseType<ErrorFullModel<AuthResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        try
        {
            var session = await auth.RefreshAsync(Request.Cookies[RefreshCookie], ClientIp, UserAgent, ct);
            SetRefreshCookie(session.RefreshToken, session.RefreshExpiresAt);
            return Success(session.Response);
        }
        catch (Exception ex)
        {
            DeleteRefreshCookie();
            return HandleException(ex);
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType<ErrorFullModel<object>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        try
        {
            await auth.LogoutAsync(Request.Cookies[RefreshCookie], ct);
            DeleteRefreshCookie();
            return Success(null, "auth_logout.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<ErrorFullModel<MeResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        try
        {
            return Success(await auth.MeAsync(ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private string? UserAgent => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    private CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        // Always Secure in production: behind the Cloudflare Tunnel Kestrel sees plain HTTP, so Request.IsHttps
        // depends on X-Forwarded-Proto. Plain http://localhost only in Development.
        Secure = Request.IsHttps || !env.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1/auth",
        Expires = expires,
        MaxAge = expires is null ? TimeSpan.Zero : TimeSpan.FromDays(options.Value.RefreshTokenDays),
    };

    private void SetRefreshCookie(string token, DateTimeOffset expires) => Response.Cookies.Append(RefreshCookie, token, CookieOptions(expires));

    private void DeleteRefreshCookie() => Response.Cookies.Delete(RefreshCookie, CookieOptions(null));
}
