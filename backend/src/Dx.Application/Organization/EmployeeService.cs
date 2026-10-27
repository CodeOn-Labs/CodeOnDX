using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dx.Application.Auth;
using Dx.Application.Common;
using Dx.Application.MasterData;
using Dx.Application.Settings;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dx.Application.Organization;

public sealed record EmployeeFilter(
    string? Search, IReadOnlyCollection<string> Positions, IReadOnlyCollection<Guid> LevelIds,
    IReadOnlyCollection<Guid> DepartmentIds, IReadOnlyCollection<Guid> TeamIds,
    IReadOnlyCollection<string> Allocation, IReadOnlyCollection<string> Statuses, bool Deleted);

public sealed record UnitRefDto(Guid Id, string Name);

public sealed record EmployeeProjectDto(Guid Id, string Code, string Name, string Role, decimal Fte);

public sealed record EmployeeAccountDto(string Status, string Email, DateTime? LastLoginAt);

public sealed record LeaveBalanceDto(int Year, decimal EntitledDays, decimal CarriedOverDays, decimal AdjustedDays);

public record EmployeeDto(
    Guid Id, string Code, string FullName, string Email, string? Phone,
    MasterRefDto? Position, MasterRefDto? Level, UnitRefDto? Department, UnitRefDto? Team, MasterRefDto? EmploymentStatus,
    DateOnly? JoinedDate, DateOnly? LeftDate, string? AvatarUrl, IReadOnlyList<string> Skills,
    IReadOnlyList<EmployeeProjectDto> Projects, decimal TotalFte, int AllocationPercent, string AllocationStatus,
    EmployeeAccountDto? Account, bool IsDeleted, DateTime CreatedAt);

/// <summary>GET /employees/{id}: the list row plus the leave fund of the current year (null when none).</summary>
public sealed record EmployeeDetailDto : EmployeeDto
{
    public EmployeeDetailDto(EmployeeDto employee, LeaveBalanceDto? leaveBalance) : base(employee) => LeaveBalance = leaveBalance;

    public LeaveBalanceDto? LeaveBalance { get; init; }
}

public sealed record EmployeeSummaryDto(int Total, int InProjects, int Bench, int Overloaded, decimal UtilizationPercent, decimal UtilizationTarget);

public sealed record SaveEmployeeRequest(
    string? FullName, string? Email, string? Code, string? Phone, string? PositionCode, string? LevelId, string? StatusCode,
    string? DepartmentId, string? TeamId, string? JoinedDate, string? LeftDate, IReadOnlyList<string?>? Skills);

/// <summary>
/// Admin › Employees (D4: HR records live in the product). An employee may or may not have a
/// login account; "invite" e-mails a one-time link to create it (account_tokens, purpose invitation).
/// </summary>
public sealed partial class EmployeeService(
    IAppDbContext db, ICurrentUser user, RuleService rules, SettingService settings, ITenantClock clock,
    IEmailSender email, IOptions<EmailOptions> emailOptions)
{
    public const string AllocationResigned = "resigned";
    public const string AllocationOverloaded = "overloaded";
    public const string AllocationBench = "bench";
    public const string AllocationLow = "low";
    public const string AllocationNormal = "normal";

    /// <summary>
    /// Allocation status from the FTE currently assigned (1 FTE = 100 %):
    ///   not working (deleted, or status metadata.working = false) → resigned
    ///   percent &gt; HR_OVERLOAD_ALLOCATION_THRESHOLD (default 100)  → overloaded   e.g. 1.2 FTE = 120 %
    ///   percent ≤ 0                                               → bench
    ///   percent &lt; HR_MIN_ALLOCATION_THRESHOLD (default 20)       → low          e.g. 0.1 FTE = 10 %
    ///   otherwise                                                 → normal
    /// </summary>
    public static string AllocationStatusOf(int percent, bool working, decimal overload, decimal min)
    {
        if (!working) return AllocationResigned;
        if (percent > overload) return AllocationOverloaded;
        if (percent <= 0) return AllocationBench;
        if (percent < min) return AllocationLow;
        return AllocationNormal;
    }

    /// <summary>employee_status.metadata.working — missing means working.</summary>
    public static bool IsWorkingStatus(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return true;
        using var doc = JsonDocument.Parse(metadataJson);
        return !(doc.RootElement.ValueKind == JsonValueKind.Object
                 && doc.RootElement.TryGetProperty("working", out var w) && w.ValueKind == JsonValueKind.False);
    }

    /// <summary>GET /employees — by code.</summary>
    public async Task<PagedResult<EmployeeDto>> SearchAsync(EmployeeFilter filter, PageRequest paging, CancellationToken ct)
        => Paging.Of(await FilteredAsync(filter, ct), paging);

    /// <summary>
    /// GET /employees/summary — over the same filter:
    ///   inProjects  = employees with at least one assignment running today
    ///   utilization = Σ min(1, totalFte) ÷ number of working employees × 100, one decimal
    ///                 e.g. (1.0 + 0.5 + 1.0 capped from 1.2) ÷ 4 working = 62.5 %
    ///   target      = rule HR_UTILIZATION_TARGET_PERCENT (default 85)
    /// </summary>
    public async Task<EmployeeSummaryDto> SummaryAsync(EmployeeFilter filter, CancellationToken ct)
    {
        var rows = await FilteredAsync(filter, ct);
        var working = rows.Where(r => r.AllocationStatus != AllocationResigned).ToList();
        var used = working.Sum(r => Math.Min(1m, r.TotalFte));
        return new EmployeeSummaryDto(
            rows.Count,
            rows.Count(r => r.Projects.Count > 0),
            rows.Count(r => r.AllocationStatus == AllocationBench),
            rows.Count(r => r.AllocationStatus == AllocationOverloaded),
            working.Count == 0 ? 0 : Math.Round(used / working.Count * 100, 1, MidpointRounding.AwayFromZero),
            await rules.GetNumberAsync(RuleKeys.HrUtilizationTargetPercent, 85, ct));
    }

    /// <summary>GET /employees/{id} — also returns deleted employees (trash view) and the leave fund of this year.</summary>
    public async Task<EmployeeDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var dto = (await LoadAsync(AllEmployees().Where(e => e.Id == id), ct)).SingleOrDefault() ?? throw new NotFoundException();
        var year = (await clock.TodayAsync(ct)).Year;
        var balance = await db.Set<LeaveBalance>().AsNoTracking().FirstOrDefaultAsync(b => b.EmployeeId == id && b.Year == year, ct);
        return new EmployeeDetailDto(dto,
            balance is null ? null : new LeaveBalanceDto(balance.Year, balance.EntitledDays, balance.CarriedOverDays, balance.AdjustedDays));
    }

    public async Task<EmployeeDto> CreateAsync(SaveEmployeeRequest request, CancellationToken ct)
    {
        var row = new Employee { Skills = [] };
        await ApplyAsync(row, request, ct);
        db.Set<Employee>().Add(row);
        await db.SaveChangesAsync(ct);
        return await ReloadAsync(row.Id, ct);
    }

    public async Task<EmployeeDto> UpdateAsync(Guid id, SaveEmployeeRequest request, CancellationToken ct)
    {
        var row = await FindAsync(id, ct);
        await ApplyAsync(row, request, ct);
        await db.SaveChangesAsync(ct);
        return await ReloadAsync(row.Id, ct);
    }

    /// <summary>DELETE /employees/{id} — to the trash; refused while an assignment is running today.</summary>
    public async Task<string> DeleteAsync(Guid id, CancellationToken ct)
    {
        var row = await FindAsync(id, ct);
        var today = await clock.TodayAsync(ct);
        if (await RunningAssignments(today).AnyAsync(a => a.EmployeeId == row.Id, ct))
            throw new ConflictException("employee_delete.err_has_assignments", new() { ["name"] = row.FullName });
        row.IsDeleted = true;
        row.DeletedAt = DateTime.UtcNow;
        row.DeletedBy = user.UserId;
        await db.SaveChangesAsync(ct);
        return row.FullName;
    }

    public async Task<EmployeeDto> RestoreAsync(Guid id, CancellationToken ct)
    {
        var row = await FindAsync(id, ct);
        row.IsDeleted = false;
        row.DeletedAt = null;
        row.DeletedBy = null;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            throw new ConflictException(); // an active employee now uses the same code / e-mail
        }
        return await ReloadAsync(row.Id, ct);
    }

    /// <summary>
    /// POST /employees/{id}/invite — e-mails a link /auth/reset-password/{token} valid for the
    /// tenant setting invitation_expiry_hours (default 72 h). A new invitation replaces the pending one.
    /// Only the SHA-256 of the token is stored.
    /// </summary>
    public async Task<EmployeeDto> InviteAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<Employee>().FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException();
        if (row.UserId is not null) throw new ConflictException("employee_invite.err_has_account");

        var tenantId = user.TenantId;
        var pending = await db.Set<AccountToken>()
            .Where(t => t.TenantId == tenantId && t.EmployeeId == row.Id && t.Purpose == AccountTokenPurposes.Invitation && t.ConsumedAt == null)
            .ToListAsync(ct);
        db.Set<AccountToken>().RemoveRange(pending);

        var hours = await settings.GetIntAsync("invitation_expiry_hours", 72, ct);
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        db.Set<AccountToken>().Add(new AccountToken
        {
            TenantId = tenantId,
            Purpose = AccountTokenPurposes.Invitation,
            TokenHash = AuthService.HashToken(token),
            Email = row.Email,
            EmployeeId = row.Id,
            ExpiresAt = clock.UtcNow.UtcDateTime.AddHours(hours),
        });
        await db.SaveChangesAsync(ct);

        var link = $"{emailOptions.Value.PublicBaseUrl.TrimEnd('/')}/auth/reset-password/{token}";
        await email.SendAsync(new EmailMessage(row.Email, AccountTokenPurposes.Invitation,
            new Dictionary<string, string> { ["full_name"] = row.FullName, ["link"] = link, ["hours"] = hours.ToString() }, user.Culture), ct);
        return await ReloadAsync(row.Id, ct);
    }

    // ─── queries ─────────────────────────────────────────────────────────────

    private IQueryable<Employee> AllEmployees() => db.Set<Employee>().IgnoreQueryFilters([QueryFilters.SoftDelete]);

    private async Task<Employee> FindAsync(Guid id, CancellationToken ct)
        => await AllEmployees().FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException();

    private async Task<EmployeeDto> ReloadAsync(Guid id, CancellationToken ct)
        => (await LoadAsync(AllEmployees().Where(e => e.Id == id), ct)).Single();

    /// <summary>Assignments running today (start ≤ today ≤ end) of projects not in the trash.</summary>
    private IQueryable<ProjectAssignment> RunningAssignments(DateOnly today)
        => db.Set<ProjectAssignment>().AsNoTracking().Where(a => a.StartDate <= today && a.EndDate >= today && !a.Project.IsDeleted);

    private async Task<List<EmployeeDto>> FilteredAsync(EmployeeFilter f, CancellationToken ct)
    {
        var query = AllEmployees().Where(e => e.IsDeleted == f.Deleted);
        if (f.Positions.Count > 0) query = query.Where(e => e.Position != null && f.Positions.Contains(e.Position.Code));
        if (f.LevelIds.Count > 0) query = query.Where(e => e.LevelId != null && f.LevelIds.Contains(e.LevelId.Value));
        if (f.DepartmentIds.Count > 0) query = query.Where(e => e.DepartmentId != null && f.DepartmentIds.Contains(e.DepartmentId.Value));
        if (f.TeamIds.Count > 0) query = query.Where(e => e.TeamId != null && f.TeamIds.Contains(e.TeamId.Value));
        if (f.Statuses.Count > 0) query = query.Where(e => f.Statuses.Contains(e.Status.Code));

        var rows = await LoadAsync(query, ct);
        return rows
            .Where(e => Text.Matches(f.Search, e.FullName, e.Email, e.Code))
            .Where(e => f.Allocation.Count == 0 || f.Allocation.Contains(e.AllocationStatus))
            .OrderBy(e => e.Code, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Builds the DTOs:
    ///   totalFte          = Σ fte_allocated of the assignments running today, 2 decimals
    ///   allocationPercent = round(totalFte × 100)
    ///   allocationStatus  = AllocationStatusOf(percent, working, HR_OVERLOAD…, HR_MIN…)
    ///   account           = login account (active / disabled), else a pending invitation (invited), else null
    /// </summary>
    private async Task<List<EmployeeDto>> LoadAsync(IQueryable<Employee> query, CancellationToken ct)
    {
        var employees = await query
            .Include(e => e.Position).Include(e => e.Level).Include(e => e.Status)
            .Include(e => e.Department).Include(e => e.Team).Include(e => e.User)
            .AsNoTracking().AsSplitQuery().ToListAsync(ct);
        var ids = employees.Select(e => e.Id).ToList();

        var today = await clock.TodayAsync(ct);
        var assignments = await RunningAssignments(today).Where(a => ids.Contains(a.EmployeeId))
            .Select(a => new
            {
                a.EmployeeId, a.FteAllocated, ProjectId = a.Project.Id, ProjectCode = a.Project.Code, ProjectName = a.Project.Name,
                PositionName = a.Position.Name, PositionNameEn = a.Position.NameEn,
            })
            .ToListAsync(ct);
        var tenantId = user.TenantId;
        var invited = (await db.Set<AccountToken>().AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Purpose == AccountTokenPurposes.Invitation && t.ConsumedAt == null
                        && t.EmployeeId != null && ids.Contains(t.EmployeeId.Value))
            .Select(t => new { EmployeeId = t.EmployeeId!.Value, t.Email }).ToListAsync(ct))
            .GroupBy(t => t.EmployeeId).ToDictionary(g => g.Key, g => g.First().Email);

        var overload = await rules.GetNumberAsync(RuleKeys.HrOverloadAllocationThreshold, 100, ct);
        var min = await rules.GetNumberAsync(RuleKeys.HrMinAllocationThreshold, 20, ct);
        var culture = user.Culture;

        return employees.Select(e =>
        {
            var mine = assignments.Where(a => a.EmployeeId == e.Id).ToList();
            var totalFte = Math.Round(mine.Sum(a => a.FteAllocated), 2, MidpointRounding.AwayFromZero);
            var percent = (int)Math.Round(totalFte * 100, MidpointRounding.AwayFromZero);
            var working = !e.IsDeleted && IsWorkingStatus(e.Status.Metadata);
            var projects = mine.GroupBy(a => a.ProjectId).Select(g => g.First())
                .Select(a => new EmployeeProjectDto(a.ProjectId, a.ProjectCode, a.ProjectName, Localized.Name(culture, a.PositionName, a.PositionNameEn), a.FteAllocated))
                .ToList();
            EmployeeAccountDto? account = e.User is { } u ? new EmployeeAccountDto(u.IsActive ? "active" : "disabled", u.Email, u.LastLoginAt)
                : invited.TryGetValue(e.Id, out var invitedEmail) ? new EmployeeAccountDto("invited", invitedEmail, null)
                : null;

            return new EmployeeDto(
                e.Id, e.Code, e.FullName, e.Email, e.Phone,
                MasterDataService.Ref(e.Position, culture), MasterDataService.Ref(e.Level, culture),
                e.Department is null ? null : new UnitRefDto(e.Department.Id, e.Department.Name),
                e.Team is null ? null : new UnitRefDto(e.Team.Id, e.Team.Name),
                MasterDataService.Ref(e.Status, culture),
                e.HireDate, e.TerminationDate, e.AvatarUrl, e.Skills,
                projects, totalFte, percent, AllocationStatusOf(percent, working, overload, min),
                account, e.IsDeleted, e.CreatedAt);
        }).ToList();
    }

    // ─── validation ──────────────────────────────────────────────────────────

    private async Task ApplyAsync(Employee row, SaveEmployeeRequest r, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var fullName = Text.Clean(r.FullName);
        var emailAddress = Text.Clean(r.Email).ToLowerInvariant();
        var code = Text.Clean(r.Code).ToUpperInvariant();
        var phone = Text.Clean(r.Phone);

        if (errors.Required("fullName", fullName)) errors.MaxLength("fullName", fullName, 150);
        if (errors.Required("email", emailAddress))
        {
            if (!Text.IsEmail(emailAddress)) errors.Add("email", "validation.invalid_email");
            else if (await AllEmployees().AnyAsync(e => e.Email.ToLower() == emailAddress && e.Id != row.Id, ct))
                errors.Add("email", "employee_save.err_duplicate_email");
        }
        if (errors.Required("code", code))
        {
            if (!CodePattern().IsMatch(code)) errors.Add("code", "employee_save.err_code_format");
            else if (await AllEmployees().AnyAsync(e => e.Code == code && e.Id != row.Id, ct)) errors.Add("code", "employee_save.err_duplicate_code");
        }
        if (phone.Length > 0 && !PhonePattern().IsMatch(phone)) errors.Add("phone", "validation.invalid_value");

        var positionCode = Text.Clean(r.PositionCode);
        MasterDatum? position = null;
        if (errors.Required("positionCode", positionCode))
        {
            position = await MasterByCodeAsync(MasterDataTypes.JobPosition, positionCode, ct);
            if (position is null) errors.Add("positionCode", "validation.invalid_value");
        }
        var levelId = errors.Id("levelId", r.LevelId);
        if (levelId is { } lv && !await db.Set<MasterDatum>().AnyAsync(m => m.Id == lv && m.Type == MasterDataTypes.HrLevel, ct))
            errors.Add("levelId", "validation.invalid_value");
        var statusCode = Text.OrNull(r.StatusCode) ?? MasterDataCodes.EmployeeStatusActive;
        var status = await MasterByCodeAsync(MasterDataTypes.EmployeeStatus, statusCode, ct);
        if (status is null) errors.Add("statusCode", "validation.invalid_value");

        var departmentId = errors.Id("departmentId", r.DepartmentId);
        if (departmentId is { } dep && !await db.Set<Department>().AnyAsync(d => d.Id == dep, ct)) errors.Add("departmentId", "validation.invalid_value");
        var teamId = errors.Id("teamId", r.TeamId);
        if (teamId is { } tm)
        {
            var team = await db.Set<Team>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == tm, ct);
            if (team is null) errors.Add("teamId", "validation.invalid_value");
            else if (departmentId is { } d && team.DepartmentId != d) errors.Add("teamId", "employee_save.err_team_department");
        }

        var joined = errors.Date("joinedDate", r.JoinedDate);
        var left = errors.Date("leftDate", r.LeftDate, optional: true);
        if (joined is { } j && left is { } l && l < j) errors.Add("leftDate", "employee_save.err_left_before_joined");
        errors.ThrowIfAny();

        row.FullName = fullName;
        row.Email = emailAddress;
        row.Code = code;
        row.Phone = Text.OrNull(phone);
        row.PositionId = position!.Id;
        row.LevelId = levelId;
        row.StatusId = status!.Id;
        row.DepartmentId = departmentId;
        row.TeamId = teamId;
        row.HireDate = joined;
        row.TerminationDate = left;
        row.Skills = (r.Skills ?? []).Select(Text.Clean).Where(s => s.Length > 0).Take(30).ToList();
    }

    private Task<MasterDatum?> MasterByCodeAsync(string type, string code, CancellationToken ct)
        => db.Set<MasterDatum>().AsNoTracking().FirstOrDefaultAsync(m => m.Type == type && m.Code == code, ct);

    [GeneratedRegex("^[A-Z0-9_-]{1,20}$")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^[0-9+\-\s]{6,20}$")]
    private static partial Regex PhonePattern();
}

/// <summary>Values of dx.account_tokens.purpose.</summary>
public static class AccountTokenPurposes
{
    public const string Invitation = "invitation";
    public const string PasswordReset = "password_reset";
}
