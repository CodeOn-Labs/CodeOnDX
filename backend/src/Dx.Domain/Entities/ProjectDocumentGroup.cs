using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectDocumentGroup
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual ICollection<ProjectDocument> ProjectDocuments { get; set; } = new List<ProjectDocument>();

    public virtual Tenant Tenant { get; set; } = null!;
}
