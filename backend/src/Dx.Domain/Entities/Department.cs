using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class Department
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public Guid? ManagerEmployeeId { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual Employee? ManagerEmployee { get; set; }

    public virtual ICollection<ProjectCostCenter> ProjectCostCenters { get; set; } = new List<ProjectCostCenter>();

    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();

    public virtual Tenant Tenant { get; set; } = null!;
}
