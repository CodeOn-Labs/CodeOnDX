using Dx.Application.Common;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Work;

public sealed record WorkEmployeeRefDto(Guid Id, string Code, string FullName, string Email, string? AvatarUrl);

/// <summary>
/// Whose time data the current user may see (timesheets, leave / OT approval):
///   timesheet.all_view or timesheet.all_edit → every employee (not deleted)
///   otherwise themself, plus with timesheet.team or timesheet.approve: members of the teams
///   they lead, of the departments they manage and of the projects where they are project manager.
/// Editing someone else's timesheet needs timesheet.all_edit.
/// </summary>
public sealed class WorkScope(IAppDbContext db, ICurrentUser user)
{
    private Guid? _me;
    private bool _meLoaded;
    private HashSet<Guid>? _visible;

    /// <summary>HR record of the signed-in user, if any.</summary>
    public async Task<Guid?> MyEmployeeIdAsync(CancellationToken ct)
    {
        if (_meLoaded) return _me;
        var userId = user.UserId;
        _me = await db.Set<Employee>().AsNoTracking().Where(e => e.UserId == userId).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);
        _meLoaded = true;
        return _me;
    }

    /// <summary>409 work_common.err_no_employee when the account has no HR record.</summary>
    public async Task<Guid> RequireMyEmployeeIdAsync(CancellationToken ct)
        => await MyEmployeeIdAsync(ct) ?? throw new ConflictException("work_common.err_no_employee");

    public async Task<IReadOnlySet<Guid>> VisibleEmployeeIdsAsync(CancellationToken ct)
    {
        if (_visible is not null) return _visible;
        var me = await MyEmployeeIdAsync(ct);
        if (user.Has(Permissions.TimesheetAllView) || user.Has(Permissions.TimesheetAllEdit))
            return _visible = (await db.Set<Employee>().AsNoTracking().Select(e => e.Id).ToListAsync(ct)).ToHashSet();

        var ids = new HashSet<Guid>();
        if (me is { } m)
        {
            ids.Add(m);
            if (user.Has(Permissions.TimesheetTeam) || user.Has(Permissions.TimesheetApprove))
            {
                ids.UnionWith(await db.Set<Employee>().AsNoTracking()
                    .Where(e => (e.Team != null && e.Team.LeaderEmployeeId == m) || (e.Department != null && e.Department.ManagerEmployeeId == m))
                    .Select(e => e.Id).ToListAsync(ct));
                ids.UnionWith(await db.Set<ProjectMember>().AsNoTracking()
                    .Where(pm => db.Set<ProjectMember>().Any(mine => mine.ProjectId == pm.ProjectId && mine.EmployeeId == m && mine.IsManager))
                    .Select(pm => pm.EmployeeId).ToListAsync(ct));
            }
        }
        return _visible = ids;
    }

    public async Task EnsureCanViewAsync(Guid employeeId, CancellationToken ct)
    {
        if (!(await VisibleEmployeeIdsAsync(ct)).Contains(employeeId)) throw new ForbiddenException();
    }

    public async Task<bool> CanEditTimesheetOfAsync(Guid employeeId, CancellationToken ct)
        => employeeId == await MyEmployeeIdAsync(ct)
            ? user.Has(Permissions.TimesheetEdit) || user.Has(Permissions.TimesheetAllEdit)
            : user.Has(Permissions.TimesheetAllEdit);

    public static WorkEmployeeRefDto Ref(Employee e) => new(e.Id, e.Code, e.FullName, e.Email, e.AvatarUrl);
}

/// <summary>Calendar helpers for the time screens (weekend = Saturday / Sunday).</summary>
public static class WorkDates
{
    public static bool IsWeekend(DateOnly d) => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>Monday of the week containing <paramref name="d"/>.</summary>
    public static DateOnly WeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public static IEnumerable<DateOnly> Range(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1)) yield return d;
    }

    public static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>yyyy-MM-dd or null.</summary>
    public static DateOnly? Parse(string? s)
        => DateOnly.TryParseExact(s?.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d : null;

    public static string Hm(TimeOnly t) => t.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    public static decimal Round2(decimal n) => Math.Round(n, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Values of timesheet_entries.source as the API shows them (the DB says overtime_request).</summary>
public static class TimesheetSources
{
    public const string Manual = "manual";
    public const string Leave = "leave_request";
    public const string Overtime = "overtime_request";

    public static string ToApi(string source) => source == Overtime ? "ot_request" : source;
}
