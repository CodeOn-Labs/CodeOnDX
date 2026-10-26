using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectAssignmentPeriod
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid AssignmentId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal FteAllocated { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ProjectAssignment Assignment { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
