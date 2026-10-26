using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class EffortAllocation
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid ProjectId { get; set; }

    public DateOnly PeriodMonth { get; set; }

    public Guid? TeamId { get; set; }

    public Guid PositionId { get; set; }

    public string? Note { get; set; }

    public decimal ContractEffort { get; set; }

    public decimal ActualEffort { get; set; }

    public decimal OvertimeEffort { get; set; }

    public Guid? PerformanceLevelId { get; set; }

    public decimal PerformanceCoefficient { get; set; }

    public short AllocationRate { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual MasterDatum? PerformanceLevel { get; set; }

    public virtual MasterDatum Position { get; set; } = null!;

    public virtual Project Project { get; set; } = null!;

    public virtual Team? Team { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
