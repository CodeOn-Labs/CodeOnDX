using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class TimesheetEntry
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid TaskTypeId { get; set; }

    public DateOnly WorkDate { get; set; }

    public decimal Hours { get; set; }

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public string Source { get; set; } = null!;

    public Guid? SourceRequestId { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual Project? Project { get; set; }

    public virtual MasterDatum TaskType { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
