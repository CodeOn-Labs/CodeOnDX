using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class TenantRule
{
    public Guid TenantId { get; set; }

    public string RuleKey { get; set; } = null!;

    public string Value { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual SystemRule RuleKeyNavigation { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
