using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectQualityMetric
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public DateOnly PeriodMonth { get; set; }

    public string Kind { get; set; } = null!;

    public int DefectCount { get; set; }

    public decimal? ProductSize { get; set; }

    public string? ProductSizeUnit { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
