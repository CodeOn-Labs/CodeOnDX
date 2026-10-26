using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class AiInsightResult
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ConfigId { get; set; }

    public string ScopeKey { get; set; } = null!;

    public string Locale { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string? Model { get; set; }

    public string? InputHash { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public virtual AiInsightConfig Config { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
