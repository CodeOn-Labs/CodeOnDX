using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class MonthlyReportMember
{
    public Guid TenantId { get; set; }

    public Guid ReportId { get; set; }

    public Guid EmployeeId { get; set; }

    public int SortOrder { get; set; }

    public bool IsRemoved { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual MonthlyReport Report { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
