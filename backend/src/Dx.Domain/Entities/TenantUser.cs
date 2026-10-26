using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class TenantUser
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
