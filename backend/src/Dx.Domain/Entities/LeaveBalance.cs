using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class LeaveBalance
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid EmployeeId { get; set; }

    public short Year { get; set; }

    public decimal EntitledDays { get; set; }

    public decimal CarriedOverDays { get; set; }

    public decimal AdjustedDays { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
