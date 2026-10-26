namespace Dx.Application.Common;

/// <summary>
/// Names of the EF Core global query filters (named filters, EF Core 10). Ignore one with
/// <c>IgnoreQueryFilters([QueryFilters.SoftDelete])</c>, e.g. for trash views; the tenant filter
/// is never ignored in application code.
/// </summary>
public static class QueryFilters
{
    public const string Tenant = "tenant";
    public const string SoftDelete = "soft_delete";
}
