using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class Team
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid DepartmentId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public Guid? LeaderEmployeeId { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<EffortAllocation> EffortAllocations { get; set; } = new List<EffortAllocation>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual Employee? LeaderEmployee { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
