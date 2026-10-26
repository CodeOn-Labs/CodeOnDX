using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectMember
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid? PositionId { get; set; }

    public bool IsManager { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual MasterDatum? Position { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
