using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class Project
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public Guid? ClientId { get; set; }

    public Guid? GradeId { get; set; }

    public Guid StatusId { get; set; }

    public Guid? RiskLevelId { get; set; }

    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public decimal? Budget { get; set; }

    public string? CurrencyCode { get; set; }

    public short ProgressPercent { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    public virtual Client? Client { get; set; }

    public virtual ICollection<EffortAllocation> EffortAllocations { get; set; } = new List<EffortAllocation>();

    public virtual MasterDatum? Grade { get; set; }

    public virtual ICollection<MonthlyReport> MonthlyReports { get; set; } = new List<MonthlyReport>();

    public virtual ICollection<OvertimeRequest> OvertimeRequests { get; set; } = new List<OvertimeRequest>();

    public virtual ICollection<ProjectAssignment> ProjectAssignments { get; set; } = new List<ProjectAssignment>();

    public virtual ICollection<ProjectChangeRequest> ProjectChangeRequests { get; set; } = new List<ProjectChangeRequest>();

    public virtual ICollection<ProjectContractStaff> ProjectContractStaffs { get; set; } = new List<ProjectContractStaff>();

    public virtual ProjectCostCenter? ProjectCostCenter { get; set; }

    public virtual ProjectCssForm? ProjectCssForm { get; set; }

    public virtual ICollection<ProjectCssQuestion> ProjectCssQuestions { get; set; } = new List<ProjectCssQuestion>();

    public virtual ICollection<ProjectCssSurvey> ProjectCssSurveys { get; set; } = new List<ProjectCssSurvey>();

    public virtual ICollection<ProjectDocumentGroup> ProjectDocumentGroups { get; set; } = new List<ProjectDocumentGroup>();

    public virtual ICollection<ProjectDocument> ProjectDocuments { get; set; } = new List<ProjectDocument>();

    public virtual ICollection<ProjectIssueLink> ProjectIssueLinks { get; set; } = new List<ProjectIssueLink>();

    public virtual ICollection<ProjectLink> ProjectLinks { get; set; } = new List<ProjectLink>();

    public virtual ICollection<ProjectMember> ProjectMembers { get; set; } = new List<ProjectMember>();

    public virtual ICollection<ProjectMilestone> ProjectMilestones { get; set; } = new List<ProjectMilestone>();

    public virtual ICollection<ProjectPcvAssessment> ProjectPcvAssessments { get; set; } = new List<ProjectPcvAssessment>();

    public virtual ICollection<ProjectPcvItem> ProjectPcvItems { get; set; } = new List<ProjectPcvItem>();

    public virtual ICollection<ProjectQualityMetric> ProjectQualityMetrics { get; set; } = new List<ProjectQualityMetric>();

    public virtual ICollection<ProjectRisk> ProjectRisks { get; set; } = new List<ProjectRisk>();

    public virtual ProjectStatusReport? ProjectStatusReport { get; set; }

    public virtual MasterDatum? RiskLevel { get; set; }

    public virtual MasterDatum Status { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<TimesheetEntry> TimesheetEntries { get; set; } = new List<TimesheetEntry>();
}
