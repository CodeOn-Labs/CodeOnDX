using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectStatusReport
{
    public Guid ProjectId { get; set; }

    public Guid TenantId { get; set; }

    public string OverallStatus { get; set; } = null!;

    public string Items { get; set; } = null!;

    public string? AiSummary { get; set; }

    public DateTime? AiGeneratedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
