using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class MonthlyReport
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public DateOnly PeriodMonth { get; set; }

    public string? ClientName { get; set; }

    public string? CompanyName { get; set; }

    public string? OrderNumber { get; set; }

    public string? WorkName { get; set; }

    public string? ResponsiblePerson { get; set; }

    public string Language { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<MonthlyReportColumn> MonthlyReportColumns { get; set; } = new List<MonthlyReportColumn>();

    public virtual ICollection<MonthlyReportEntry> MonthlyReportEntries { get; set; } = new List<MonthlyReportEntry>();

    public virtual ICollection<MonthlyReportMember> MonthlyReportMembers { get; set; } = new List<MonthlyReportMember>();

    public virtual Project Project { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
