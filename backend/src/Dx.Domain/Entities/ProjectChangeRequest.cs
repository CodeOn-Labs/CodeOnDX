using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectChangeRequest
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public string Code { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Url { get; set; }

    public string Source { get; set; } = null!;

    public string ImpactLevel { get; set; } = null!;

    public List<string> ImpactAreas { get; set; } = null!;

    public string? ImpactNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
