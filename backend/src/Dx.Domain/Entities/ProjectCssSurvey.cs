using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class ProjectCssSurvey
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public DateOnly PeriodMonth { get; set; }

    public string Token { get; set; } = null!;

    public string Status { get; set; } = null!;

    public bool IsRevoked { get; set; }

    public string? SentTo { get; set; }

    public Guid? SentBy { get; set; }

    public DateTime SentAt { get; set; }

    public string? Respondent { get; set; }

    public string? Comment { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual ICollection<ProjectCssAnswer> ProjectCssAnswers { get; set; } = new List<ProjectCssAnswer>();

    public virtual User? SentByNavigation { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
