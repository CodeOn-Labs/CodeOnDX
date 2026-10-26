using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectCostCenter
{
    public Guid ProjectId { get; set; }

    public Guid TenantId { get; set; }

    public Guid DepartmentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual Project Project { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
