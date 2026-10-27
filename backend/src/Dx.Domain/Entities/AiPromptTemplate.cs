using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class AiPromptTemplate
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Scope { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Template { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public string Status { get; set; } = null!;

    public List<string> ScreenKeys { get; set; } = null!;

    public string? Description { get; set; }

    public int Version { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
