using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectRisk
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public string Code { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string Category { get; set; } = null!;

    public short Probability { get; set; }

    public short Impact { get; set; }

    public string Strategy { get; set; } = null!;

    public string? ResponsePlan { get; set; }

    public Guid? OwnerEmployeeId { get; set; }

    public DateOnly DueDate { get; set; }

    public string Status { get; set; } = null!;

    public DateOnly IdentifiedDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    public virtual Employee? OwnerEmployee { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
