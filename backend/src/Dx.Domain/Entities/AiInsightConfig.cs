using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class AiInsightConfig
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string ScreenKey { get; set; } = null!;

    public string Name { get; set; } = null!;

    public List<string> DataSources { get; set; } = null!;

    /// <summary>
    /// Empty array = visible to every role.
    /// </summary>
    public List<Guid> AllowedRoleIds { get; set; } = null!;

    public string DataPrompt { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<AiInsightResult> AiInsightResults { get; set; } = new List<AiInsightResult>();

    public virtual Tenant Tenant { get; set; } = null!;
}
