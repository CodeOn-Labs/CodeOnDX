using System.Globalization;
using Dx.Application.Common;

namespace Dx.Api.Common;

/// <summary>
/// Query-string helpers matching the mock API contract: list parameters accept
/// ?ids=a,b and ?ids=a&amp;ids=b; dates are yyyy-MM-dd; invalid values become field errors.
/// </summary>
public static class QueryParams
{
    public static List<string> List(IQueryCollection query, string name)
        => query[name].SelectMany(v => (v ?? string.Empty).Split(',')).Select(v => v.Trim()).Where(v => v.Length > 0).ToList();

    public static List<Guid> Guids(IQueryCollection query, string name, ValidationErrors errors)
    {
        var result = new List<Guid>();
        foreach (var raw in List(query, name))
        {
            if (Guid.TryParse(raw, out var id)) result.Add(id);
            else { errors.Add(name, "validation.invalid_value"); break; }
        }
        return result;
    }

    public static DateOnly? Date(IQueryCollection query, string name, ValidationErrors errors)
    {
        var raw = query[name].ToString();
        if (string.IsNullOrEmpty(raw)) return null;
        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        errors.Add(name, "validation.invalid_date");
        return null;
    }

    public static PageRequest Paging(IQueryCollection query, int defaultPageSize = 20)
    {
        var page = int.TryParse(query["page"], out var p) ? p : 1;
        var pageSize = query.ContainsKey("pageSize") ? (int.TryParse(query["pageSize"], out var s) ? s : 0) : defaultPageSize;
        return new PageRequest(page, pageSize);
    }
}
