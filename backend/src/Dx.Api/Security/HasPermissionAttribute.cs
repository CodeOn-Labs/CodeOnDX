using Dx.Api.Common;
using Dx.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;

namespace Dx.Api.Security;

/// <summary>
/// [HasPermission("project.view")] — requires that permission key in the access token (D3).
/// Several attributes on one action = all required. Per-project checks (member / project
/// manager) are done in the services.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(PolicyPrefix + permission)
{
    public const string PolicyPrefix = "perm:";

    public string Permission { get; } = permission;
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(DxClaims.Permission, requirement.Permission)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>Builds "perm:&lt;key&gt;" policies on the fly so no policy has to be registered by hand.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[HasPermissionAttribute.PolicyPrefix.Length..]))
            .Build();
    }
}

/// <summary>Safety net: authorization failures answer ErrorFullModel 401 / 403 instead of an empty body.</summary>
public sealed class ErrorFullModelAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (ErrorResponses.IsApi(context))
        {
            if (authorizeResult.Challenged) return ErrorResponses.WriteAsync(context, 401, "error.unauthorized");
            if (authorizeResult.Forbidden) return ErrorResponses.WriteAsync(context, 403, "error.forbidden");
        }
        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
