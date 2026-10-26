using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectPcvItem
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? TemplateItemId { get; set; }

    public string Category { get; set; } = null!;

    public string Content { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual ICollection<ProjectPcvResult> ProjectPcvResults { get; set; } = new List<ProjectPcvResult>();

    public virtual PcvTemplateItem? TemplateItem { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
