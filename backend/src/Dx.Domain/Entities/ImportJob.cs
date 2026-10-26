using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ImportJob
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Kind { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string? FilePath { get; set; }

    public int TotalRows { get; set; }

    public int SucceededRows { get; set; }

    public int FailedRows { get; set; }

    public string Errors { get; set; } = null!;

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
