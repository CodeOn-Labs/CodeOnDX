using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class MasterDatum
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Type { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? NameEn { get; set; }

    public string? Description { get; set; }

    public string Metadata { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<Client> ClientCountries { get; set; } = new List<Client>();

    public virtual ICollection<Client> ClientIndustries { get; set; } = new List<Client>();

    public virtual ICollection<EffortAllocation> EffortAllocationPerformanceLevels { get; set; } = new List<EffortAllocation>();

    public virtual ICollection<EffortAllocation> EffortAllocationPositions { get; set; } = new List<EffortAllocation>();

    public virtual ICollection<Employee> EmployeeLevels { get; set; } = new List<Employee>();

    public virtual ICollection<Employee> EmployeePositions { get; set; } = new List<Employee>();

    public virtual ICollection<Employee> EmployeeStatuses { get; set; } = new List<Employee>();

    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();

    public virtual ICollection<ProjectAssignment> ProjectAssignmentLevels { get; set; } = new List<ProjectAssignment>();

    public virtual ICollection<ProjectAssignment> ProjectAssignmentPositions { get; set; } = new List<ProjectAssignment>();

    public virtual ICollection<ProjectContractStaff> ProjectContractStaffLevels { get; set; } = new List<ProjectContractStaff>();

    public virtual ICollection<ProjectContractStaff> ProjectContractStaffPositions { get; set; } = new List<ProjectContractStaff>();

    public virtual ICollection<Project> ProjectGrades { get; set; } = new List<Project>();

    public virtual ICollection<ProjectLink> ProjectLinks { get; set; } = new List<ProjectLink>();

    public virtual ICollection<ProjectMember> ProjectMembers { get; set; } = new List<ProjectMember>();

    public virtual ICollection<Project> ProjectRiskLevels { get; set; } = new List<Project>();

    public virtual ICollection<Project> ProjectStatuses { get; set; } = new List<Project>();

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<TimesheetEntry> TimesheetEntries { get; set; } = new List<TimesheetEntry>();
}
