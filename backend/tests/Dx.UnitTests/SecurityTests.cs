using System.Security.Claims;
using Dx.Api.Security;
using Dx.Application.Common;
using Dx.Domain.Entities;
using Dx.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Shouldly;

namespace Dx.UnitTests;

public sealed class JwtAccessTokenIssuerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 8, 0, 0, TimeSpan.Zero);
    private readonly JwtOptions _options = new() { Issuer = "dx.test", Audience = "dx-spa", SigningKey = new string('k', 48), AccessTokenMinutes = 15 };

    [Fact]
    public async Task Token_carries_user_tenant_employee_and_permissions_and_validates_with_the_key()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "a@example.test", FullName = "A" };
        var tenant = Guid.NewGuid();
        var employee = Guid.NewGuid();
        var issuer = new JwtAccessTokenIssuer(Options.Create(_options), new FakeTimeProvider(Now));

        var access = issuer.Issue(user, tenant, employee, ["project.view", "timesheet.edit"]);

        access.ExpiresIn.ShouldBe(900);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(access.Token, new TokenValidationParameters
        {
            ValidIssuer = "dx.test",
            ValidAudience = "dx-spa",
            IssuerSigningKey = JwtAccessTokenIssuer.SigningKey(_options),
            ValidateLifetime = false,
        });
        result.IsValid.ShouldBeTrue();

        var jwt = (JsonWebToken)result.SecurityToken;
        jwt.GetClaim(DxClaims.Subject).Value.ShouldBe(user.Id.ToString());
        jwt.GetClaim(DxClaims.Tenant).Value.ShouldBe(tenant.ToString());
        jwt.GetClaim(DxClaims.Employee).Value.ShouldBe(employee.ToString());
        jwt.GetClaim(DxClaims.Email).Value.ShouldBe("a@example.test");
        jwt.Claims.Where(c => c.Type == DxClaims.Permission).Select(c => c.Value).ShouldBe(["project.view", "timesheet.edit"]);
        jwt.ValidTo.ShouldBe(Now.UtcDateTime.AddMinutes(15));
        jwt.Alg.ShouldBe(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public void Token_without_employee_has_no_emp_claim()
    {
        var issuer = new JwtAccessTokenIssuer(Options.Create(_options), new FakeTimeProvider(Now));
        var access = issuer.Issue(new User { Id = Guid.NewGuid(), Email = "b@example.test", FullName = "B" }, Guid.NewGuid(), null, []);
        new JsonWebTokenHandler().ReadJsonWebToken(access.Token).TryGetClaim(DxClaims.Employee, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var issuer = new JwtAccessTokenIssuer(Options.Create(_options), new FakeTimeProvider(Now));
        var access = issuer.Issue(new User { Id = Guid.NewGuid(), Email = "c@example.test", FullName = "C" }, Guid.NewGuid(), null, []);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(access.Token, new TokenValidationParameters
        {
            ValidIssuer = "dx.test",
            ValidAudience = "dx-spa",
            IssuerSigningKey = JwtAccessTokenIssuer.SigningKey(new JwtOptions { SigningKey = new string('x', 48) }),
            ValidateLifetime = false,
        });
        result.IsValid.ShouldBeFalse();
    }
}

public sealed class IdentityPasswordHasherTests
{
    private readonly IdentityPasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_verify_round_trips()
    {
        var user = new User();
        user.PasswordHash = _hasher.Hash(user, "Demo@2026");

        user.PasswordHash.ShouldStartWith("AQAAAAIAAYag"); // Identity V3, 100 000 iterations
        _hasher.Verify(user, "Demo@2026", out var rehash).ShouldBeTrue();
        rehash.ShouldBeFalse();
        _hasher.Verify(user, "demo@2026", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void User_without_password_never_verifies(string? hash)
        => _hasher.Verify(new User { PasswordHash = hash }, "anything", out _).ShouldBeFalse();
}

public sealed class RequestContextTests
{
    private static RequestContext For(ClaimsPrincipal principal, string? acceptLanguage = null)
    {
        var http = new DefaultHttpContext { User = principal };
        if (acceptLanguage is not null) http.Request.Headers.AcceptLanguage = acceptLanguage;
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(http);
        return new RequestContext(accessor);
    }

    [Fact]
    public void Reads_user_tenant_and_permissions_from_claims()
    {
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var ctx = For(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(DxClaims.Subject, user.ToString()),
            new Claim(DxClaims.Tenant, tenant.ToString()),
            new Claim(DxClaims.Permission, "project.view"),
            new Claim(DxClaims.Permission, "admin.settings"),
        ], authenticationType: "Bearer")), "en-US,en;q=0.9");

        ctx.IsAuthenticated.ShouldBeTrue();
        ctx.UserId.ShouldBe(user);
        ctx.TenantId.ShouldBe(tenant);
        ((ITenantContext)ctx).TenantId.ShouldBe(tenant);
        ctx.Permissions.ShouldBe(["project.view", "admin.settings"], ignoreOrder: true);
        ((ICurrentUser)ctx).Has("project.view").ShouldBeTrue();
        ((ICurrentUser)ctx).Has("project.manage").ShouldBeFalse();
        ctx.Culture.ShouldBe("en");
    }

    [Fact]
    public void Anonymous_request_has_no_tenant_until_one_is_used()
    {
        var ctx = For(new ClaimsPrincipal(new ClaimsIdentity()));
        var tenant = (ITenantContext)ctx;

        ctx.IsAuthenticated.ShouldBeFalse();
        tenant.TenantId.ShouldBeNull();
        Should.Throw<UnauthorizedException>(() => ctx.UserId);
        Should.Throw<UnauthorizedException>(() => ctx.TenantId);
        ctx.Permissions.ShouldBeEmpty();
        ctx.Culture.ShouldBe("vi");

        var id = Guid.NewGuid();
        tenant.Use(id);
        tenant.TenantId.ShouldBe(id);
        ctx.TenantId.ShouldBe(id);
    }

    [Fact]
    public void Explicit_tenant_wins_over_the_token_claim()
    {
        var ctx = For(new ClaimsPrincipal(new ClaimsIdentity([new Claim(DxClaims.Tenant, Guid.NewGuid().ToString())], "Bearer")));
        var chosen = Guid.NewGuid();
        ((ITenantContext)ctx).Use(chosen);
        ctx.TenantId.ShouldBe(chosen);
    }
}
