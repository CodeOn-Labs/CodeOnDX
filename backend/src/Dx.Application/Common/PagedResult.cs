namespace Dx.Application.Common;

/// <summary>Paging contract: page starts at 1; pageSize = 0 means "all rows" (exports).</summary>
public sealed record PageRequest(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 500;

    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 0, MaxPageSize);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
