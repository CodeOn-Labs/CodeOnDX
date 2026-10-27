using System.Text.RegularExpressions;
using Dx.Application.Common;
using Dx.Domain.Constants;
using Dx.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dx.Application.Organization;

public sealed record EmployeeRefDto(Guid Id, string FullName, string Email, string? AvatarUrl);

public sealed record TeamDto(
    Guid Id, Guid DepartmentId, string Code, string Name, string? Description, EmployeeRefDto? Leader,
    int SortSeq, bool IsActive, int MemberCount);

public sealed record DepartmentDto(
    Guid Id, string Code, string Name, string? Description, EmployeeRefDto? Manager,
    int SortSeq, bool IsActive, int MemberCount, IReadOnlyList<TeamDto> Teams);

public sealed record SaveDepartmentRequest(string? Code, string? Name, string? Description, string? ManagerId, bool? IsActive);

public sealed record SaveTeamRequest(string? DepartmentId, string? Code, string? Name, string? Description, string? LeaderId, bool? IsActive);

/// <summary>
/// Admin › Departments &amp; teams (D4). memberCount counts current employees (not deleted, status
/// other than RESIGNED). A department / team that still has current members — or a
/// department that still has teams — cannot be deleted.
/// </summary>
public sealed partial class DepartmentService(IAppDbContext db)
{
    /// <summary>GET /departments — departments with their teams, by sort order.</summary>
    public async Task<IReadOnlyList<DepartmentDto>> ListAsync(CancellationToken ct)
    {
        var departments = await db.Set<Department>().AsNoTracking().Include(d => d.ManagerEmployee)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.Code).ToListAsync(ct);
        var teams = await db.Set<Team>().AsNoTracking().Include(t => t.LeaderEmployee)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Code).ToListAsync(ct);
        var members = await WorkingMembersAsync(ct);
        return departments.Select(d => ToDto(d, teams.Where(t => t.DepartmentId == d.Id), members)).ToList();
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(SaveDepartmentRequest request, CancellationToken ct)
    {
        var row = new Department { SortOrder = await db.Set<Department>().CountAsync(ct) + 1 };
        await ApplyAsync(row, request, ct);
        db.Set<Department>().Add(row);
        await db.SaveChangesAsync(ct);
        return await GetDepartmentAsync(row.Id, ct);
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(Guid id, SaveDepartmentRequest request, CancellationToken ct)
    {
        var row = await db.Set<Department>().FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException();
        await ApplyAsync(row, request, ct);
        await db.SaveChangesAsync(ct);
        return await GetDepartmentAsync(row.Id, ct);
    }

    /// <summary>Hard delete (departments have no trash). Former / deleted employees lose the link.</summary>
    public async Task<string> DeleteDepartmentAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<Department>().FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException();
        var members = await WorkingMembersAsync(ct);
        if (members.Any(m => m.DepartmentId == row.Id) || await db.Set<Team>().AnyAsync(t => t.DepartmentId == row.Id, ct))
            throw new ConflictException("department_delete.err_not_empty", new() { ["name"] = row.Name });

        await DetachEmployeesAsync(e => e.DepartmentId == row.Id, e => e.DepartmentId = null, ct);
        db.Set<Department>().Remove(row);
        await SaveOrConflictAsync("department_delete.err_not_empty", row.Name, ct);
        return row.Name;
    }

    public async Task<TeamDto> CreateTeamAsync(SaveTeamRequest request, CancellationToken ct)
    {
        var row = new Team();
        await ApplyAsync(row, request, isNew: true, ct);
        row.SortOrder = await db.Set<Team>().CountAsync(t => t.DepartmentId == row.DepartmentId, ct) + 1;
        db.Set<Team>().Add(row);
        await db.SaveChangesAsync(ct);
        return await GetTeamAsync(row.Id, ct);
    }

    /// <summary>Moving a team to another department moves its employees too.</summary>
    public async Task<TeamDto> UpdateTeamAsync(Guid id, SaveTeamRequest request, CancellationToken ct)
    {
        var row = await db.Set<Team>().FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException();
        await ApplyAsync(row, request, isNew: false, ct);
        var employees = await db.Set<Employee>().IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(e => e.TeamId == row.Id && e.DepartmentId != row.DepartmentId).ToListAsync(ct);
        foreach (var e in employees) e.DepartmentId = row.DepartmentId;
        await db.SaveChangesAsync(ct);
        return await GetTeamAsync(row.Id, ct);
    }

    public async Task<string> DeleteTeamAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<Team>().FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException();
        var members = await WorkingMembersAsync(ct);
        if (members.Any(m => m.TeamId == row.Id)) throw new ConflictException("team_delete.err_not_empty", new() { ["name"] = row.Name });

        await DetachEmployeesAsync(e => e.TeamId == row.Id, e => e.TeamId = null, ct);
        db.Set<Team>().Remove(row);
        await SaveOrConflictAsync("team_delete.err_not_empty", row.Name, ct);
        return row.Name;
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private sealed record Member(Guid? DepartmentId, Guid? TeamId);

    /// <summary>Department / team of every current employee (not deleted, not resigned).</summary>
    private async Task<List<Member>> WorkingMembersAsync(CancellationToken ct)
        => await db.Set<Employee>().AsNoTracking()
            .Where(e => e.Status.Code != MasterDataCodes.EmployeeStatusResigned)
            .Select(e => new Member(e.DepartmentId, e.TeamId)).ToListAsync(ct);

    private async Task DetachEmployeesAsync(System.Linq.Expressions.Expression<Func<Employee, bool>> where, Action<Employee> detach, CancellationToken ct)
    {
        var rows = await db.Set<Employee>().IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(where).ToListAsync(ct);
        foreach (var e in rows) detach(e);
    }

    /// <summary>Other tables (cost centers, effort allocations…) may still reference the unit.</summary>
    private async Task SaveOrConflictAsync(string key, string name, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsForeignKeyViolation(ex))
        {
            throw new ConflictException(key, new() { ["name"] = name });
        }
    }

    private async Task<DepartmentDto> GetDepartmentAsync(Guid id, CancellationToken ct)
        => (await ListAsync(ct)).Single(d => d.Id == id);

    private async Task<TeamDto> GetTeamAsync(Guid id, CancellationToken ct)
        => (await ListAsync(ct)).SelectMany(d => d.Teams).Single(t => t.Id == id);

    private async Task ApplyAsync(Department row, SaveDepartmentRequest r, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var (code, name, description) = await ValidateUnitAsync(errors, r.Code, r.Name, r.Description,
            c => db.Set<Department>().AnyAsync(d => d.Code == c && d.Id != row.Id, ct));
        var managerId = await EmployeeIdAsync(errors, "managerId", r.ManagerId, ct);
        errors.ThrowIfAny();

        row.Code = code;
        row.Name = name;
        row.Description = description;
        row.ManagerEmployeeId = managerId;
        row.IsActive = r.IsActive != false;
    }

    private async Task ApplyAsync(Team row, SaveTeamRequest r, bool isNew, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var (code, name, description) = await ValidateUnitAsync(errors, r.Code, r.Name, r.Description,
            c => db.Set<Team>().AnyAsync(t => t.Code == c && t.Id != row.Id, ct));
        var departmentId = string.IsNullOrWhiteSpace(r.DepartmentId) && !isNew ? row.DepartmentId : errors.Id("departmentId", r.DepartmentId);
        if (departmentId is not { } dep || !await db.Set<Department>().AnyAsync(d => d.Id == dep, ct))
            errors.Add("departmentId", isNew ? "validation.required" : "validation.invalid_value");
        var leaderId = await EmployeeIdAsync(errors, "leaderId", r.LeaderId, ct);
        errors.ThrowIfAny();

        row.DepartmentId = departmentId!.Value;
        row.Code = code;
        row.Name = name;
        row.Description = description;
        row.LeaderEmployeeId = leaderId;
        row.IsActive = r.IsActive != false;
    }

    private static async Task<(string Code, string Name, string? Description)> ValidateUnitAsync(
        ValidationErrors errors, string? rawCode, string? rawName, string? rawDescription, Func<string, Task<bool>> codeTaken)
    {
        var code = Text.Clean(rawCode).ToUpperInvariant();
        var name = Text.Clean(rawName);
        var description = Text.Clean(rawDescription);
        if (errors.Required("code", code))
        {
            if (!CodePattern().IsMatch(code)) errors.Add("code", "org_unit_save.err_code_format");
            else if (await codeTaken(code)) errors.Add("code", "org_unit_save.err_duplicate_code");
        }
        if (errors.Required("name", name)) errors.MaxLength("name", name, 150);
        errors.MaxLength("description", description, 500);
        return (code, name, Text.OrNull(description));
    }

    private async Task<Guid?> EmployeeIdAsync(ValidationErrors errors, string field, string? raw, CancellationToken ct)
    {
        var id = errors.Id(field, raw);
        if (id is { } v && !await db.Set<Employee>().AnyAsync(e => e.Id == v, ct))
        {
            errors.Add(field, "validation.invalid_value");
            return null;
        }
        return id;
    }

    private static EmployeeRefDto? Ref(Employee? e) => e is null ? null : new EmployeeRefDto(e.Id, e.FullName, e.Email, e.AvatarUrl);

    private static TeamDto ToDto(Team t, IReadOnlyCollection<Member> members) => new(
        t.Id, t.DepartmentId, t.Code, t.Name, t.Description, Ref(t.LeaderEmployee), t.SortOrder, t.IsActive,
        members.Count(m => m.TeamId == t.Id));

    private static DepartmentDto ToDto(Department d, IEnumerable<Team> teams, IReadOnlyCollection<Member> members) => new(
        d.Id, d.Code, d.Name, d.Description, Ref(d.ManagerEmployee), d.SortOrder, d.IsActive,
        members.Count(m => m.DepartmentId == d.Id), teams.Select(t => ToDto(t, members)).ToList());

    [GeneratedRegex("^[A-Z0-9_-]{1,30}$")]
    private static partial Regex CodePattern();
}
