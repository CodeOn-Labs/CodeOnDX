using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class TenantSetting
{
    public Guid TenantId { get; set; }

    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
