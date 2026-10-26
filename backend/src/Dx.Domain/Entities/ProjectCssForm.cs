using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectCssForm
{
    public Guid ProjectId { get; set; }

    public Guid TenantId { get; set; }

    public string Title { get; set; } = null!;

    public string Intro { get; set; } = null!;

    public string ScaleNote { get; set; } = null!;

    public string CommentLabel { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
