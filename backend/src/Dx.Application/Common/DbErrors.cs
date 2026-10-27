using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Common;

/// <summary>PostgreSQL error codes surfaced through EF Core (SQLSTATE).</summary>
public static class DbErrors
{
    /// <summary>23503 — the row is still referenced by another table.</summary>
    public static bool IsForeignKeyViolation(DbUpdateException ex) => SqlState(ex) == "23503";

    /// <summary>23505 — unique index / constraint.</summary>
    public static bool IsUniqueViolation(DbUpdateException ex) => SqlState(ex) == "23505";

    private static string? SqlState(DbUpdateException ex) => (ex.InnerException as DbException)?.SqlState;
}
