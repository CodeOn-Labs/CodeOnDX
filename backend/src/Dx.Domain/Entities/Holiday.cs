using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class Holiday
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public DateOnly HolidayDate { get; set; }

    public string Name { get; set; } = null!;

    public bool IsPaid { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
