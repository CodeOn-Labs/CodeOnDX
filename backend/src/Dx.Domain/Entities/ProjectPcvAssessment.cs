using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectPcvAssessment
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public DateOnly PeriodMonth { get; set; }

    public Guid? AssessedBy { get; set; }

    public DateTime AssessedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual User? AssessedByNavigation { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual ICollection<ProjectPcvResult> ProjectPcvResults { get; set; } = new List<ProjectPcvResult>();

    public virtual Tenant Tenant { get; set; } = null!;
}
