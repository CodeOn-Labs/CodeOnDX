using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class OvertimeRequest
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    public Guid? ProjectId { get; set; }

    public DateOnly WorkDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public decimal Hours { get; set; }

    public string DayType { get; set; } = null!;

    public short RatePercent { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public Guid? DecidedBy { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual User? DecidedByNavigation { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual Project? Project { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
