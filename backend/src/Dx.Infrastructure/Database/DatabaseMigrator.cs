using System.Reflection;
using DbUp;
using DbUp.Engine.Output;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Dx.Infrastructure.Database;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>Role the API connects as (non-owner, no BYPASSRLS); gets DML grants after migrating.</summary>
    public string AppRole { get; set; } = "dx_app";

    /// <summary>Generated demo-tenant script (tools/db-seed/generate.mjs), copied next to the app by Dx.Api.csproj.</summary>
    public string DemoSeedPath { get; set; } = "seed/demo_tenant.sql";
}

/// <summary>
/// DB-first schema deployment with DbUp. Scripts are database/migrations/V*.sql, embedded
/// into this assembly; applied ones are journaled in public.dx_schema_versions (outside schema dx so the
/// scaffolder ignores it) and never re-run,
/// so a schema change is always a new V0xx__*.sql file.
/// Runs with the owner connection string (ConnectionStrings:DxOwner), not the app role.
/// </summary>
public sealed class DatabaseMigrator(IOptions<DatabaseOptions> options, ILogger<DatabaseMigrator> logger)
{
    private const string ResourcePrefix = "migrations.";

    public bool Migrate(string ownerConnectionString)
    {

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(ownerConnectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly(), name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .JournalToPostgresqlTable("public", "dx_schema_versions")
            .WithTransactionPerScript()
            .LogTo(new DbUpLogger(logger))
            .Build();

        var pending = upgrader.GetScriptsToExecute().Select(s => s.Name).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database is up to date");
        }
        else
        {
            logger.LogInformation("Applying {Count} script(s): {Scripts}", pending.Count, string.Join(", ", pending));
            var result = upgrader.PerformUpgrade();
            if (!result.Successful)
            {
                logger.LogError(result.Error, "Migration failed at {Script}", result.ErrorScript?.Name);
                return false;
            }
        }

        GrantAppRole(ownerConnectionString);
        return true;
    }

    /// <summary>Loads the demo tenant (mock-data/seed, fictional data only — D10). Re-runnable.</summary>
    public void SeedDemo(string ownerConnectionString)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, options.Value.DemoSeedPath));
        if (!File.Exists(path)) throw new FileNotFoundException("Demo seed script not found; run node tools/db-seed/generate.mjs", path);

        using var conn = new NpgsqlConnection(ownerConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        using (var cmd = new NpgsqlCommand(File.ReadAllText(path), conn, tx)) cmd.ExecuteNonQuery();
        tx.Commit();
        logger.LogInformation("Demo tenant loaded from {Path}", path);
    }

    private void GrantAppRole(string ownerConnectionString)
    {
        var role = options.Value.AppRole;
        if (string.IsNullOrWhiteSpace(role)) return;
        var quoted = new NpgsqlCommandBuilder().QuoteIdentifier(role);
        Execute(ownerConnectionString, $"""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{role.Replace("'", "''")}') THEN
                    GRANT USAGE ON SCHEMA dx TO {quoted};
                    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA dx TO {quoted};
                    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA dx TO {quoted};
                    GRANT EXECUTE ON FUNCTION dx.current_tenant_id() TO {quoted};
                    GRANT EXECUTE ON FUNCTION dx.find_css_survey_tenant_id(text) TO {quoted};
                END IF;
            END
            $$;
            """);
    }

    private static void Execute(string connectionString, string sql)
    {
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    private sealed class DbUpLogger(ILogger logger) : IUpgradeLog
    {
        public void LogTrace(string format, params object[] args) => logger.LogTrace(format, args);
        public void LogDebug(string format, params object[] args) => logger.LogDebug(format, args);
        public void LogInformation(string format, params object[] args) => logger.LogInformation(format, args);
        public void LogWarning(string format, params object[] args) => logger.LogWarning(format, args);
        public void LogError(string format, params object[] args) => logger.LogError(format, args);
        public void LogError(Exception ex, string format, params object[] args) => logger.LogError(ex, format, args);
    }
}
