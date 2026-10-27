using Dx.Application.Common;
using Dx.Application.MasterData;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Projects;

public sealed record ProjectFilter(
    string? Search,
    IReadOnlyCollection<Guid> StatusIds,
    IReadOnlyCollection<Guid> GradeIds,
    IReadOnlyCollection<Guid> ClientIds,
    IReadOnlyCollection<Guid> RiskLevelIds,
    DateOnly? StartFrom,
    DateOnly? EndTo,
    bool Deleted);

public sealed record ClientRefDto(Guid Id, string Name);

public sealed record ProjectListItemDto(
    Guid Id, string Code, string Name, ClientRefDto? Client,
    MasterRefDto? Grade, MasterRefDto? Status, MasterRefDto? RiskLevel,
    DateOnly? StartDate, DateOnly? EndDate, decimal? Budget, string? CurrencyCode, int ProgressPercent,
    decimal ContractStaffFte, decimal ActualStaffFte, decimal BudgetUsedPercent,
    bool IsDeleted, DateTime CreatedAt);

public sealed record ProjectSummaryDto(int Total, int Running, int EndingSoon, int Planning, int Risky, int EndingSoonDays);

public sealed class ProjectService(IAppDbContext db, ICurrentUser user, RuleService rules, ITenantClock clock)
{
    /// <summary>
    /// Standard working days in one FTE-month. Actual cost FTE = approved project hours ÷
    /// (TIMESHEET_FTE_HOURS × this). With the default 8 h → 160 h per FTE-month, the same
    /// divisor the original product used.
    /// </summary>
    public const int WorkingDaysPerFteMonth = 20;

    /// <summary>GET /projects — filtered, newest first. Without project.manage only own projects.</summary>
    public async Task<PagedResult<ProjectListItemDto>> SearchAsync(ProjectFilter filter, PageRequest paging, CancellationToken ct)
    {
        var query = Filtered(filter).OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id);
        var total = await query.CountAsync(ct);
        var pageSize = paging.SafePageSize;
        var page = pageSize == 0 ? 1 : paging.SafePage;
        var pageQuery = pageSize == 0 ? query : query.Skip((page - 1) * pageSize).Take(pageSize);

        var projects = await pageQuery
            .Include(p => p.Client).Include(p => p.Grade).Include(p => p.Status).Include(p => p.RiskLevel)
            .AsNoTracking().ToListAsync(ct);
        var items = await ToListItemsAsync(projects, ct);
        return new PagedResult<ProjectListItemDto>(items, page, pageSize, total);
    }

    /// <summary>
    /// GET /projects/summary — KPI cards over the same filter.
    ///   running / planning : status code RUNNING / PLANNING
    ///   risky              : risk level code HIGH
    ///   endingSoon         : end date not passed and (status ENDING or ends within
    ///                        PROJECT_ENDING_SOON_DAYS days of today, tenant time zone)
    /// </summary>
    public async Task<ProjectSummaryDto> SummaryAsync(ProjectFilter filter, CancellationToken ct)
    {
        var endingSoonDays = (int)await rules.GetNumberAsync(RuleKeys.ProjectEndingSoonDays, 30, ct);
        var today = await clock.TodayAsync(ct);
        var rows = await Filtered(filter)
            .Select(p => new { StatusCode = p.Status.Code, RiskCode = p.RiskLevel != null ? p.RiskLevel.Code : null, p.EndDate })
            .ToListAsync(ct);

        var endingSoon = rows.Count(r =>
        {
            if (r.EndDate is not { } end) return false;
            var left = end.DayNumber - today.DayNumber;
            return left >= 0 && (r.StatusCode == MasterDataCodes.ProjectStatusEnding || left <= endingSoonDays);
        });
        return new ProjectSummaryDto(
            rows.Count,
            rows.Count(r => r.StatusCode == MasterDataCodes.ProjectStatusRunning),
            endingSoon,
            rows.Count(r => r.StatusCode == MasterDataCodes.ProjectStatusPlanning),
            rows.Count(r => r.RiskCode == MasterDataCodes.RiskLevelHigh),
            endingSoonDays);
    }

    private IQueryable<Project> Filtered(ProjectFilter f)
    {
        // Soft-deleted rows are only listed in the trash view (deleted=true).
        var query = db.Set<Project>().IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(p => p.IsDeleted == f.Deleted);

        if (!user.Has(Permissions.ProjectManage))
        {
            var me = user.UserId;
            query = query.Where(p => p.ProjectMembers.Any(m => m.Employee.UserId == me));
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var pattern = $"%{EscapeLike(f.Search.Trim())}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern, "\\"));
        }
        if (f.StatusIds.Count > 0) query = query.Where(p => f.StatusIds.Contains(p.StatusId));
        if (f.GradeIds.Count > 0) query = query.Where(p => p.GradeId != null && f.GradeIds.Contains(p.GradeId.Value));
        if (f.ClientIds.Count > 0) query = query.Where(p => p.ClientId != null && f.ClientIds.Contains(p.ClientId.Value));
        if (f.RiskLevelIds.Count > 0) query = query.Where(p => p.RiskLevelId != null && f.RiskLevelIds.Contains(p.RiskLevelId.Value));
        if (f.StartFrom is { } from) query = query.Where(p => p.StartDate != null && p.StartDate >= from);
        if (f.EndTo is { } to) query = query.Where(p => p.EndDate != null && p.EndDate <= to);
        return query;
    }

    /// <summary>
    /// Per-project figures computed from staffing and timesheets (not stored, D12):
    ///   contractStaffFte  = Σ quantity × fte_per_person            (project_contract_staff)
    ///                       e.g. 2 devs × 1.0 + 1 PM × 0.5 = 2.5
    ///   actualStaffFte    = Σ fte_allocated of assignments running today (start ≤ today ≤ end,
    ///                       tenant time zone), so finished projects show 0  (project_assignments)
    ///   actualCostFte     = Σ approved hours ÷ (TIMESHEET_FTE_HOURS × 20)
    ///                       e.g. 480 h ÷ (8 × 20) = 3 FTE-months
    ///   avgMonthlyRate    = Σ (monthly_rate × quantity) ÷ Σ quantity
    ///   budgetUsedPercent = actualCostFte × avgMonthlyRate ÷ total × 100, rounded to 2 decimals,
    ///                       where total = budget when budget &gt; 1000 (an amount of money),
    ///                       otherwise budget × avgMonthlyRate (budget given in man-months).
    ///                       e.g. 3 × 4 000 ÷ 383 000 × 100 = 3.13
    /// </summary>
    internal async Task<IReadOnlyList<ProjectListItemDto>> ToListItemsAsync(IReadOnlyList<Project> projects, CancellationToken ct)
    {
        var ids = projects.Select(p => p.Id).ToList();
        var staff = await db.Set<ProjectContractStaff>().AsNoTracking()
            .Where(s => ids.Contains(s.ProjectId))
            .GroupBy(s => s.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                Fte = g.Sum(s => s.Quantity * s.FtePerPerson),
                Quantity = g.Sum(s => (int)s.Quantity),
                RateWeighted = g.Sum(s => (s.MonthlyRate ?? 0) * s.Quantity),
            })
            .ToDictionaryAsync(x => x.ProjectId, ct);
        var today = await clock.TodayAsync(ct);
        var assigned = await db.Set<ProjectAssignment>().AsNoTracking()
            .Where(a => ids.Contains(a.ProjectId) && a.StartDate <= today && a.EndDate >= today)
            .GroupBy(a => a.ProjectId)
            .Select(g => new { ProjectId = g.Key, Fte = g.Sum(a => a.FteAllocated) })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Fte, ct);
        var hours = await db.Set<TimesheetEntry>().AsNoTracking()
            .Where(t => t.ProjectId != null && ids.Contains(t.ProjectId.Value) && t.Status == "approved")
            .GroupBy(t => t.ProjectId!.Value)
            .Select(g => new { ProjectId = g.Key, Hours = g.Sum(t => t.Hours) })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Hours, ct);

        var fteHours = await rules.GetNumberAsync(RuleKeys.TimesheetFteHours, 8, ct);
        var hoursPerFteMonth = fteHours * WorkingDaysPerFteMonth;
        var culture = user.Culture;

        return projects.Select(p =>
        {
            staff.TryGetValue(p.Id, out var s);
            var avgRate = s is { Quantity: > 0 } ? s.RateWeighted / s.Quantity : 0m;
            var actualCostFte = hoursPerFteMonth > 0 ? hours.GetValueOrDefault(p.Id) / hoursPerFteMonth : 0m;
            var budget = p.Budget ?? 0m;
            var total = budget > 1000 ? budget : budget * avgRate;
            var budgetUsed = total == 0 ? 0m : Math.Round(actualCostFte * avgRate / total * 100, 2);

            return new ProjectListItemDto(
                p.Id, p.Code, p.Name,
                p.Client is null ? null : new ClientRefDto(p.Client.Id, p.Client.Name),
                MasterDataService.Ref(p.Grade, culture), MasterDataService.Ref(p.Status, culture), MasterDataService.Ref(p.RiskLevel, culture),
                p.StartDate, p.EndDate, p.Budget, p.CurrencyCode, p.ProgressPercent,
                Math.Round(s?.Fte ?? 0, 2), Math.Round(assigned.GetValueOrDefault(p.Id), 2), budgetUsed,
                p.IsDeleted, p.CreatedAt);
        }).ToList();
    }

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
