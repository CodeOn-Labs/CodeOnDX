using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class MonthlyReportColumn
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ReportId { get; set; }

    public string ColumnKey { get; set; } = null!;

    public string? Source { get; set; }

    public string Label { get; set; } = null!;

    public string? LabelJa { get; set; }

    public string DataType { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual MonthlyReport Report { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
