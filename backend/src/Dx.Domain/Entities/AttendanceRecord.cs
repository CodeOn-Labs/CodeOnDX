using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class AttendanceRecord
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid EmployeeId { get; set; }

    public DateOnly WorkDate { get; set; }

    public TimeOnly? CheckIn { get; set; }

    public TimeOnly? CheckOut { get; set; }

    public string Source { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
