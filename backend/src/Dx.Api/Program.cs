using System.Threading.RateLimiting;
using Dx.Api.Common;
using Dx.Api.Security;
using Dx.Application;
using Dx.Application.Common;
using Dx.Infrastructure;
using Dx.Infrastructure.Database;
using Dx.Infrastructure.Persistence;
using Dx.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

// Command line:
//   Dx.Api                 run the API (Windows Service "DxApi" in production, D8)
//   Dx.Api --migrate       apply database/migrations with ConnectionStrings:DxOwner, then exit
//   Dx.Api --seed-demo     migrate + load the demo tenant from database/seed/demo_tenant.sql, then exit
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null,
});

// Secrets (Jwt:SigningKey, connection passwords, connector keys) never live in git.
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: false);
builder.Services.AddWindowsService(o => o.ServiceName = "DxApi");
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<RequestContext>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<RequestContext>());
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<RequestContext>());
builder.Services.AddSingleton<JsonStringLocalizer>();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// ─── MVC + safety net for model binding errors (400) ─────────────────────────
builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = ctx =>
{
    var localizer = ctx.HttpContext.RequestServices.GetRequiredService<JsonStringLocalizer>();
    var data = ctx.ModelState
        .Where(e => e.Value?.Errors.Count > 0)
        .ToDictionary(
            e => FieldName(e.Key),
            e => new[] { localizer.Text("validation.invalid_value") });
    return new ObjectResult(new ErrorFullModel { Status = 400, MessageText = localizer.Text("error.validation_failed"), Data = data }) { StatusCode = 400 };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

// ─── Authentication (JWT access token, D2) + permission policies (D3) ──────────
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = JwtAccessTokenIssuer.SigningKey(jwt),
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = DxClaims.Name,
    };
    o.Events = new JwtBearerEvents
    {
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse();
            await ErrorResponses.WriteAsync(ctx.HttpContext, 401, "error.unauthorized");
        },
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ErrorFullModelAuthorizationResultHandler>();

// ─── Rate limiting (429) ───────────────────────────────────────────────────────
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy(RateLimitPolicies.Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
    o.OnRejected = (ctx, _) => new ValueTask(ErrorResponses.WriteAsync(ctx.HttpContext, 429, "error.too_many_requests"));
});

// Behind the machine's Cloudflare Tunnel (D9): trust X-Forwarded-For / -Proto from loopback only.
builder.Services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();

// ─── One-shot commands ─────────────────────────────────────────────────────────
if (args.Contains("--migrate") || args.Contains("--seed-demo"))
{
    var owner = app.Configuration.GetConnectionString("DxOwner")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:DxOwner (schema owner) for --migrate");
    var migrator = app.Services.GetRequiredService<DatabaseMigrator>();
    if (!migrator.Migrate(owner)) return 1;
    if (args.Contains("--seed-demo")) migrator.SeedDemo(owner);
    return 0;
}

// ─── Pipeline ──────────────────────────────────────────────────────────────────
app.UseForwardedHeaders();
app.UseExceptionHandler(_ => { }); // handled by GlobalExceptionHandler
app.UseSerilogRequestLogging();
app.UseStatusCodePages(async ctx =>
{
    // Safety net: unknown route / wrong method under /api → 404 / 405 ErrorFullModel.
    var http = ctx.HttpContext;
    if (!ErrorResponses.IsApi(http) || http.Response.ContentLength > 0) return;
    var key = http.Response.StatusCode switch
    {
        404 => "error.not_found",
        405 => "error.method_not_allowed",
        401 => "error.unauthorized",
        403 => "error.forbidden",
        _ => "error.internal",
    };
    await ErrorResponses.WriteAsync(http, http.Response.StatusCode, key);
});
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers.XFrameOptions = "DENY";
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", async (DxDbContext db, JsonStringLocalizer l, CancellationToken ct) =>
{
    var dbOk = await db.Database.CanConnectAsync(ct);
    var status = dbOk ? 200 : 503;
    return Results.Json(new ErrorFullModel
    {
        Status = status,
        MessageText = l.Text(dbOk ? "common.msg_success" : "error.internal"),
        Data = new { status = dbOk ? "ok" : "degraded", mode = "api", database = dbOk },
    }, statusCode: status);
}).AllowAnonymous();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("OpenApi:Expose", false))
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs");
}

// SPA (Vue build copied to wwwroot); never answers /api/* — those fall to the 404 safety net.
app.MapFallbackToFile("{*path:regex(^(?!api/).*$):nonfile}", "index.html");

app.Run();
return 0;

// "$.email" / "request.email" → "email"
static string FieldName(string key)
{
    var name = key.StartsWith("$.") ? key[2..] : key;
    var dot = name.LastIndexOf('.');
    if (dot >= 0) name = name[(dot + 1)..];
    return name.Length == 0 ? "body" : char.ToLowerInvariant(name[0]) + name[1..];
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
}

public partial class Program;
