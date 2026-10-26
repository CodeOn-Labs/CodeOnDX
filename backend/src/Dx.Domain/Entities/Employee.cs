using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class Employee
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string Code { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public Guid? PositionId { get; set; }

    public Guid? LevelId { get; set; }

    public Guid? DepartmentId { get; set; }

    public Guid? TeamId { get; set; }

    public Guid StatusId { get; set; }

    public DateOnly? HireDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public string? AvatarUrl { get; set; }

    public List<string> Skills { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    public virtual ICollection<AccountToken> AccountTokens { get; set; } = new List<AccountToken>();

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    public virtual Department? Department { get; set; }

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    public virtual ICollection<EffortAllocation> EffortAllocations { get; set; } = new List<EffortAllocation>();

    public virtual ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();

    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();

    public virtual MasterDatum? Level { get; set; }

    public virtual ICollection<MonthlyReportEntry> MonthlyReportEntries { get; set; } = new List<MonthlyReportEntry>();

    public virtual ICollection<MonthlyReportMember> MonthlyReportMembers { get; set; } = new List<MonthlyReportMember>();

    public virtual ICollection<OvertimeRequest> OvertimeRequests { get; set; } = new List<OvertimeRequest>();

    public virtual MasterDatum? Position { get; set; }

    public virtual ICollection<ProjectAssignment> ProjectAssignments { get; set; } = new List<ProjectAssignment>();

    public virtual ICollection<ProjectMember> ProjectMembers { get; set; } = new List<ProjectMember>();

    public virtual ICollection<ProjectRisk> ProjectRisks { get; set; } = new List<ProjectRisk>();

    public virtual MasterDatum Status { get; set; } = null!;

    public virtual Team? Team { get; set; }

    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<TimesheetEntry> TimesheetEntries { get; set; } = new List<TimesheetEntry>();

    public virtual User? User { get; set; }
}
