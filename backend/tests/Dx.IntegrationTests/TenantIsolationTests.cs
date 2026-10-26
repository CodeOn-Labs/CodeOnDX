using Dx.IntegrationTests.Infrastructure;
using Npgsql;
using Shouldly;

namespace Dx.IntegrationTests;

/// <summary>
/// D1 second line of defence: PostgreSQL RLS for the app role dx_app, independent of the
/// EF Core filter (V011). Queried directly with the same connection string the API uses.
/// </summary>
public sealed class TenantIsolationTests(ApiFixture fx)
{
    [Fact]
    public async Task Without_app_tenant_id_the_app_role_sees_no_tenant_rows()
    {
        await using var conn = await OpenAppAsync(tenant: null);
        (await CountAsync(conn, "dx.projects")).ShouldBe(0);
        (await CountAsync(conn, "dx.master_data")).ShouldBe(0);
        (await CountAsync(conn, "dx.tenant_settings")).ShouldBe(0);
    }

    [Fact]
    public async Task With_app_tenant_id_the_app_role_sees_only_that_tenant()
    {
        await using var conn = await OpenAppAsync(BetaTenant.TenantId);
        (await CountAsync(conn, "dx.projects")).ShouldBe(5);
        (await CountAsync(conn, "dx.projects WHERE tenant_id <> '" + BetaTenant.TenantId + "'")).ShouldBe(0);
        (await CountAsync(conn, "dx.master_data")).ShouldBe(11);
    }

    [Fact]
    public async Task App_role_cannot_write_rows_of_another_tenant()
    {
        var demoTenant = await fx.ScalarOwnerAsync<Guid>("SELECT id FROM dx.tenants WHERE code = 'demo'");
        await using var conn = await OpenAppAsync(BetaTenant.TenantId);
        await using var cmd = new NpgsqlCommand(
            $"INSERT INTO dx.tenant_settings (tenant_id, key, value) VALUES ('{demoTenant}', 'hacked', 'x')", conn);
        var ex = await Should.ThrowAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        ex.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege); // new row violates row-level security policy
    }

    [Fact]
    public async Task Global_tables_stay_readable_before_the_tenant_is_known()
    {
        await using var conn = await OpenAppAsync(tenant: null);
        (await CountAsync(conn, "dx.users")).ShouldBeGreaterThan(0);
        (await CountAsync(conn, "dx.tenant_users")).ShouldBeGreaterThan(0);
        (await CountAsync(conn, "dx.system_rules")).ShouldBeGreaterThan(0);
    }

    private async Task<NpgsqlConnection> OpenAppAsync(Guid? tenant)
    {
        var conn = new NpgsqlConnection(fx.AppConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT set_config('app.tenant_id', @t, false)", conn);
        cmd.Parameters.AddWithValue("t", tenant?.ToString() ?? string.Empty);
        await cmd.ExecuteNonQueryAsync();
        return conn;
    }

    private static async Task<long> CountAsync(NpgsqlConnection conn, string from)
    {
        await using var cmd = new NpgsqlCommand($"SELECT count(*) FROM {from}", conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
