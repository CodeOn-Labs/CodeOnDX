using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dx.Infrastructure.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Dx.IntegrationTests.Infrastructure;

/// <summary>
/// One per test run (assembly fixture): throw-away PostgreSQL cluster → roles dx_owner / dx_app
/// like production → DbUp migrations + demo tenant + <see cref="BetaTenant"/> → the real API
/// in memory (WebApplicationFactory) connected as dx_app, so RLS applies exactly as deployed.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string AppPassword = "dx_app_test";
    public const string DemoPassword = "Demo@2026";

    private readonly ConcurrentDictionary<string, string> _tokens = new();
    private PostgresCluster? _cluster;
    private string _contentRoot = string.Empty;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public string OwnerConnectionString { get; private set; } = string.Empty;
    public string AppConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _cluster = await PostgresCluster.StartAsync();
        var admin = _cluster.ConnectionString("postgres", PostgresCluster.Superuser);
        await ExecuteAsync(admin, $"""
            CREATE ROLE dx_owner LOGIN;
            CREATE ROLE dx_app LOGIN PASSWORD '{AppPassword}';
            CREATE DATABASE dx OWNER dx_owner;
            """);
        OwnerConnectionString = _cluster.ConnectionString("dx", "dx_owner");
        AppConnectionString = _cluster.ConnectionString("dx", "dx_app") + $";Password={AppPassword}";

        var migrator = new DatabaseMigrator(Options.Create(new DatabaseOptions()), NullLogger<DatabaseMigrator>.Instance);
        if (!migrator.Migrate(OwnerConnectionString)) throw new InvalidOperationException("Migration failed");
        migrator.SeedDemo(OwnerConnectionString);
        await BetaTenant.SeedAsync(OwnerConnectionString);

        // Empty content root: the developer's appsettings*.json / appsettings.Secrets.json are not
        // loaded (Program adds the secrets file last, which would win over test settings).
        _contentRoot = Path.Combine(Path.GetTempPath(), "dx-it-root-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(_contentRoot);

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseContentRoot(_contentRoot);
            b.UseSetting("ConnectionStrings:Dx", AppConnectionString);
            b.UseSetting("ConnectionStrings:DxOwner", OwnerConnectionString);
            b.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789-abcdefghijklmnop");
            b.UseSetting("Jwt:Issuer", "dx.test");
            b.UseSetting("Jwt:Audience", "dx-spa");
            b.UseSetting("Jwt:AccessTokenMinutes", "15");
            b.ConfigureServices(s => s.AddSingleton<IStartupFilter, TestClientIpStartupFilter>());
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        if (_cluster is not null) await _cluster.DisposeAsync();
        try { Directory.Delete(_contentRoot, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>Client with no token. <paramref name="clientIp"/> fixes the caller IP (throttle tests); otherwise each request gets its own IP.</summary>
    public HttpClient Client(string? culture = null, string? clientIp = null)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        if (culture is not null) client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture));
        if (clientIp is not null) client.DefaultRequestHeaders.Add(TestClientIpStartupFilter.Header, clientIp);
        return client;
    }

    /// <summary>Client signed in as <paramref name="email"/> (access token cached for the run).</summary>
    public async Task<HttpClient> ClientAs(string email, string? culture = null)
    {
        var client = Client(culture);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(email));
        return client;
    }

    public async Task<string> TokenAsync(string email)
    {
        if (_tokens.TryGetValue(email, out var cached)) return cached;
        var password = email.EndsWith("@example.com", StringComparison.Ordinal) ? DemoPassword : BetaTenant.Password;
        using var response = await Client().PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var body = await Envelope.ReadAsync(response, HttpStatusCode.OK);
        var token = body.GetProperty("data").GetProperty("accessToken").GetString()!;
        return _tokens[email] = token;
    }

    public async Task ExecuteOwnerAsync(string sql) => await ExecuteAsync(OwnerConnectionString, sql);

    public async Task<T> ScalarOwnerAsync<T>(string sql)
    {
        await using var conn = new NpgsqlConnection(OwnerConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return (T)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        foreach (var statement in sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // CREATE DATABASE cannot run inside a multi-statement batch.
            await using var cmd = new NpgsqlCommand(statement, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

/// <summary>
/// TestServer has no remote IP, so every request would share one rate-limit / login-throttle
/// partition. Give each request its own IP unless the test pins one with X-Test-Client-Ip.
/// </summary>
internal sealed class TestClientIpStartupFilter : IStartupFilter
{
    public const string Header = "X-Test-Client-Ip";
    private static int _counter;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (ctx, nextMiddleware) =>
        {
            ctx.Connection.RemoteIpAddress = ctx.Request.Headers.TryGetValue(Header, out var ip) && IPAddress.TryParse(ip, out var parsed)
                ? parsed
                : new IPAddress(BitConverter.GetBytes(0x0A000000 + Interlocked.Increment(ref _counter)).Reverse().ToArray());
            await nextMiddleware();
        });
        next(app);
    };
}

/// <summary>Response helpers for the ErrorFullModel envelope (Q2).</summary>
public static class Envelope
{
    /// <summary>Asserts HTTP status == body.status == expected and returns the parsed body.</summary>
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != expected)
            throw new Xunit.Sdk.XunitException($"Expected HTTP {(int)expected} but got {(int)response.StatusCode}: {text}");
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement.Clone();
        if (root.GetProperty("status").GetInt32() != (int)expected)
            throw new Xunit.Sdk.XunitException($"Body status {root.GetProperty("status")} differs from HTTP {(int)expected}");
        if (root.GetProperty("messageText").GetString() is not { Length: > 0 })
            throw new Xunit.Sdk.XunitException("messageText is empty");
        return root;
    }

    public static Guid Id(this JsonElement e) => e.GetProperty("id").GetGuid();
}
