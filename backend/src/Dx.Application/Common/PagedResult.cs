namespace Dx.Application.Common;

/// <summary>Paging contract: page starts at 1; pageSize = 0 means "all rows" (exports).</summary>
public sealed record PageRequest(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 500;

    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 0, MaxPageSize);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class Paging
{
    /// <summary>Pages a list already filtered in memory (same rules as the mock paginate()).</summary>
    public static PagedResult<T> Of<T>(IReadOnlyList<T> rows, PageRequest request)
    {
        var pageSize = request.SafePageSize;
        var page = pageSize == 0 ? 1 : request.SafePage;
        var items = pageSize == 0 ? rows : rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<T>(items, page, pageSize, rows.Count);
    }
}
