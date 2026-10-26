using Dx.Api.Common;
using Dx.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Shouldly;

namespace Dx.UnitTests;

/// <summary>Query-string contract shared with the mock API (?ids=a,b and ?ids=a&amp;ids=b, yyyy-MM-dd, paging).</summary>
public sealed class QueryParamsTests
{
    private static QueryCollection Query(params (string Key, string[] Values)[] pairs)
        => new(pairs.ToDictionary(p => p.Key, p => new StringValues(p.Values)));

    [Fact]
    public void List_splits_commas_merges_repeats_and_drops_blanks()
    {
        var q = Query(("type", ["a, b", "c", " ", ",,d"]));
        QueryParams.List(q, "type").ShouldBe(["a", "b", "c", "d"]);
        QueryParams.List(q, "missing").ShouldBeEmpty();
    }

    [Fact]
    public void Guids_parses_valid_ids()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var errors = new ValidationErrors();
        QueryParams.Guids(Query(("ids", [$"{a},{b}"])), "ids", errors).ShouldBe([a, b]);
        errors.Any.ShouldBeFalse();
    }

    [Fact]
    public void Guids_reports_one_error_for_the_parameter_on_the_first_bad_value()
    {
        var errors = new ValidationErrors();
        QueryParams.Guids(Query(("ids", ["nope,also-bad"])), "ids", errors);

        var ex = Should.Throw<AppValidationException>(errors.ThrowIfAny);
        ex.Errors.Keys.ShouldBe(["ids"]);
        ex.Errors["ids"].Single().Key.ShouldBe("validation.invalid_value");
    }

    [Theory]
    [InlineData("2026-10-25", true)]
    [InlineData("2026-02-30", false)]
    [InlineData("25/10/2026", false)]
    [InlineData("2026-10-25T00:00:00", false)]
    public void Date_accepts_only_yyyy_MM_dd(string raw, bool valid)
    {
        var errors = new ValidationErrors();
        var date = QueryParams.Date(Query(("d", [raw])), "d", errors);

        if (valid)
        {
            date.ShouldBe(new DateOnly(2026, 10, 25));
            errors.Any.ShouldBeFalse();
        }
        else
        {
            date.ShouldBeNull();
            Should.Throw<AppValidationException>(errors.ThrowIfAny).Errors["d"].Single().Key.ShouldBe("validation.invalid_date");
        }
    }

    [Fact]
    public void Date_missing_is_null_without_error()
    {
        var errors = new ValidationErrors();
        QueryParams.Date(Query(), "d", errors).ShouldBeNull();
        errors.Any.ShouldBeFalse();
    }

    [Fact]
    public void Paging_defaults_and_parses()
    {
        QueryParams.Paging(Query()).ShouldBe(new PageRequest(1, 20));
        QueryParams.Paging(Query(), defaultPageSize: 50).ShouldBe(new PageRequest(1, 50));
        QueryParams.Paging(Query(("page", ["3"]), ("pageSize", ["10"]))).ShouldBe(new PageRequest(3, 10));
        QueryParams.Paging(Query(("page", ["x"]))).Page.ShouldBe(1);
        // pageSize present but not a number → 0 = all rows
        QueryParams.Paging(Query(("pageSize", ["all"]))).PageSize.ShouldBe(0);
    }
}

public sealed class PageRequestTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(7, 7)]
    public void SafePage_is_at_least_1(int page, int expected) => new PageRequest(page, 20).SafePage.ShouldBe(expected);

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(10_000, PageRequest.MaxPageSize)]
    public void SafePageSize_is_clamped_between_0_and_max(int size, int expected) => new PageRequest(1, size).SafePageSize.ShouldBe(expected);
}
