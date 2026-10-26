using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectLink
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid LinkTypeId { get; set; }

    public string Name { get; set; } = null!;

    public string Url { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual MasterDatum LinkType { get; set; } = null!;

    public virtual Project Project { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
