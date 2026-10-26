using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectPcvResult
{
    public Guid TenantId { get; set; }

    public Guid AssessmentId { get; set; }

    public Guid ItemId { get; set; }

    public string? Result { get; set; }

    public string? Note { get; set; }

    public virtual ProjectPcvAssessment Assessment { get; set; } = null!;

    public virtual ProjectPcvItem Item { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
