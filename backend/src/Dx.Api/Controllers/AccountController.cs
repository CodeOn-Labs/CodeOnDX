using System.Text.Json;
using Dx.Api.Common;
using Dx.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dx.Api.Controllers;

/// <summary>Self-service account: forgot / reset password, accept invitation, own profile and password (D2, D4).</summary>
[Route("api/v1")]
public sealed class AccountController(AccountService account) : ApiControllerBase
{
    /// <summary>POST /auth/forgot-password — same answer whether or not the e-mail exists.</summary>
    [HttpPost("auth/forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        try
        {
            await account.ForgotPasswordAsync(request, ct);
            return Success(null, "auth_forgot_password.msg_success",
                args: new Dictionary<string, object?> { ["minutes"] = AccountService.ResetTokenMinutes });
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>GET /auth/reset-password/{token} — 404 unknown link, 410 expired or used.</summary>
    [HttpGet("auth/reset-password/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ErrorFullModel<AccountTokenDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckToken(string token, CancellationToken ct)
    {
        try
        {
            return Success(await account.CheckTokenAsync(token, ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>POST /auth/reset-password — new password for a reset link, or account creation for an invitation.</summary>
    [HttpPost("auth/reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        try
        {
            var kind = await account.ResetPasswordAsync(request, ct);
            return Success(null, kind == AccountService.KindReset ? "auth_reset_password.msg_success" : "auth_accept_invite.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType<ErrorFullModel<ProfileDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        try
        {
            return Success(await account.GetProfileAsync(ct));
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>PUT /profile — partial: fullName, phone, preferredLocale, avatarUrl (data:image/…), skills.</summary>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType<ErrorFullModel<ProfileDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] Dictionary<string, JsonElement> request, CancellationToken ct)
    {
        try
        {
            return Success(await account.UpdateProfileAsync(request, ct), "profile_update.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost("profile/change-password")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ErrorFullModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        try
        {
            await account.ChangePasswordAsync(request, ct);
            return Success(null, "profile_change_password.msg_success");
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }
}
