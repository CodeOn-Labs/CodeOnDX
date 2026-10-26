using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class LeaveRequest
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public string DurationType { get; set; } = null!;

    public decimal TotalHours { get; set; }

    public string? Reason { get; set; }

    public string? AttachmentUrl { get; set; }

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

    public virtual MasterDatum LeaveType { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
