using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class MonthlyReportEntry
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ReportId { get; set; }

    public Guid EmployeeId { get; set; }

    public DateOnly WorkDate { get; set; }

    public decimal? Hours { get; set; }

    public string? HoursSource { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? TimeSource { get; set; }

    public string? Content { get; set; }

    public string? ContentJa { get; set; }

    public string? ContentJaSource { get; set; }

    public string CustomValues { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual MonthlyReport Report { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
