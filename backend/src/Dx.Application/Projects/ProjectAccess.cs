using Dx.Application.Common;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Projects;

/// <summary>A project opened by the current user. <see cref="EmployeeId"/> is the user's own HR record, if any.</summary>
public sealed record ProjectScope(Project Project, bool CanManage, Guid? EmployeeId);

/// <summary>
/// Per-project authorization (D3; never by role name). The controller already requires
/// project.view; on top of that:
///   open   — project.manage, or the user is assigned to the project (live assignment)
///   manage — project.manage and the project is not in the trash (409 project_detail.err_deleted)
/// </summary>
public sealed class ProjectAccess(IAppDbContext db, ICurrentUser user)
{
    public async Task<ProjectScope> OpenAsync(Guid projectId, CancellationToken ct, bool tracking = false)
    {
        var query = db.Set<Project>().IgnoreQueryFilters([QueryFilters.SoftDelete]);
        if (!tracking) query = query.AsNoTracking();
        var project = await query.FirstOrDefaultAsync(p => p.Id == projectId, ct) ?? throw new NotFoundException();
        var canManage = user.Has(Permissions.ProjectManage);
        var userId = user.UserId;
        var employeeId = await db.Set<Employee>().AsNoTracking().Where(e => e.UserId == userId).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);
        if (!canManage)
        {
            var assigned = employeeId is { } me && await db.Set<ProjectAssignment>().AnyAsync(a => a.ProjectId == projectId && a.EmployeeId == me, ct);
            if (!assigned) throw new ForbiddenException("project_detail.err_no_access");
        }
        return new ProjectScope(project, canManage, employeeId);
    }

    public async Task<ProjectScope> ManageAsync(Guid projectId, CancellationToken ct, bool tracking = false)
    {
        var scope = await OpenAsync(projectId, ct, tracking);
        if (!scope.CanManage) throw new ForbiddenException();
        if (scope.Project.IsDeleted) throw new ConflictException("project_detail.err_deleted");
        return scope;
    }
}

/// <summary>Date helpers shared by the project screens (weekdays = Monday–Friday; holidays not removed).</summary>
public static class ProjectDates
{
    public static bool IsWeekend(DateOnly d) => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>Number of Monday–Friday days in [from, to].</summary>
    public static int WorkingDaysBetween(DateOnly from, DateOnly to)
    {
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1)) if (!IsWeekend(d)) n++;
        return n;
    }

    public static IEnumerable<DateOnly> DaysOfMonth(DateOnly month)
    {
        var first = new DateOnly(month.Year, month.Month, 1);
        for (var d = first; d.Month == first.Month; d = d.AddDays(1)) yield return d;
    }

    public static int WorkingDaysOfMonth(DateOnly month) => DaysOfMonth(month).Count(d => !IsWeekend(d));

    public static string MonthKey(DateOnly d) => d.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

    public static decimal Round2(decimal n) => Math.Round(n, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Colour thresholds of the quality screens — parameters edited in Admin › Rules (module quality, D12).
/// Defaults are the original product's values.
/// </summary>
public sealed record QualityThresholds(
    decimal EeGreenMax, decimal EeOrangeMax,
    decimal DefectGoodMin, decimal DefectGoodMax, decimal DefectWarnMax,
    decimal LeakageGoodMin, decimal LeakageGoodMax, decimal LeakageWarnMax)
{
    public static async Task<QualityThresholds> LoadAsync(RuleService rules, CancellationToken ct) => new(
        await rules.GetNumberAsync("QUALITY_EE_GREEN_MAX", 100, ct), await rules.GetNumberAsync("QUALITY_EE_ORANGE_MAX", 110, ct),
        await rules.GetNumberAsync("QUALITY_DEFECT_RATE_GOOD_MIN", 4, ct), await rules.GetNumberAsync("QUALITY_DEFECT_RATE_GOOD_MAX", 5, ct),
        await rules.GetNumberAsync("QUALITY_DEFECT_RATE_WARN_MAX", 6, ct),
        await rules.GetNumberAsync("QUALITY_LEAKAGE_RATE_GOOD_MIN", 0.45m, ct), await rules.GetNumberAsync("QUALITY_LEAKAGE_RATE_GOOD_MAX", 0.5m, ct),
        await rules.GetNumberAsync("QUALITY_LEAKAGE_RATE_WARN_MAX", 0.55m, ct));

    /// <summary>
    /// EE tone: ≤ EeGreenMax (100 %) → green (used no more effort than the contract); ≤ EeOrangeMax
    /// (110 %) → orange; above → red; no EE (contract = 0) → none.
    /// </summary>
    public string EeTone(decimal? value)
        => value is not { } v ? "none" : v <= EeGreenMax ? "green" : v <= EeOrangeMax ? "orange" : "red";

    /// <summary>
    /// Defect / leakage rate tone, 4 levels: below the good range (suspiciously few bugs, maybe not
    /// enough testing) → lightOrange; inside → green; up to the warning limit → yellow; above → red.
    /// Defects per MM, defaults: &lt; 4 · 4–5 · &gt; 5–6 · &gt; 6. Leakage: &lt; 0.45 · 0.45–&lt; 0.5 · 0.5–0.55 · &gt; 0.55
    /// (the upper bound of the good leakage range is open, as in the original product).
    /// </summary>
    public string RateTone(decimal? value, string kind)
    {
        if (value is not { } v) return "none";
        if (kind == QualityKinds.Defect)
        {
            if (v < DefectGoodMin) return "lightOrange";
            if (v <= DefectGoodMax) return "green";
            return v <= DefectWarnMax ? "yellow" : "red";
        }
        if (v < LeakageGoodMin) return "lightOrange";
        if (v < LeakageGoodMax) return "green";
        return v <= LeakageWarnMax ? "yellow" : "red";
    }
}

public static class QualityKinds
{
    public const string Defect = "defect";
    public const string Leakage = "leakage";
}
