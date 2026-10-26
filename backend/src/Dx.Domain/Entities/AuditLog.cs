using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class AuditLog
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string EntityType { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public string Action { get; set; } = null!;

    public string? Changes { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? TraceId { get; set; }

    public DateTime OccurredAt { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
