using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dx.Infrastructure.Persistence;

/// <summary>
/// Sets the PostgreSQL session setting app.tenant_id each time EF opens a connection, so
/// row-level security (V011) sees the same tenant as the EF global filter. Pooled
/// connections are opened again for every use, so a stale tenant never leaks across requests.
/// </summary>
internal sealed class TenantConnectionInterceptor(DxDbContext context) : DbConnectionInterceptor
{
    private const string Sql = "SELECT set_config('app.tenant_id', @tenant, false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = CreateCommand(connection);
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var cmd = CreateCommand(connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = Sql;
        var p = cmd.CreateParameter();
        p.ParameterName = "tenant";
        var tenantId = context.CurrentTenantId;
        p.Value = tenantId == Guid.Empty ? string.Empty : tenantId.ToString();
        cmd.Parameters.Add(p);
        return cmd;
    }
}
