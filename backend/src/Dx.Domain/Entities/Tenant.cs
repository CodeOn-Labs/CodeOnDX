using System;
using System.Collections.Generic;

namespace Dx.Domain.Entities;

public partial class Tenant
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string DefaultLocale { get; set; } = null!;

    public string TimeZone { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<AccountToken> AccountTokens { get; set; } = new List<AccountToken>();

    public virtual ICollection<AiInsightConfig> AiInsightConfigs { get; set; } = new List<AiInsightConfig>();

    public virtual ICollection<AiInsightResult> AiInsightResults { get; set; } = new List<AiInsightResult>();

    public virtual ICollection<AiPromptTemplate> AiPromptTemplates { get; set; } = new List<AiPromptTemplate>();

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<Client> Clients { get; set; } = new List<Client>();

    public virtual CssTemplate? CssTemplate { get; set; }

    public virtual ICollection<CssTemplateQuestion> CssTemplateQuestions { get; set; } = new List<CssTemplateQuestion>();

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    public virtual ICollection<EffortAllocation> EffortAllocations { get; set; } = new List<EffortAllocation>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual ICollection<Holiday> Holidays { get; set; } = new List<Holiday>();

    public virtual ICollection<ImportJob> ImportJobs { get; set; } = new List<ImportJob>();

    public virtual ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();

    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();

    public virtual ICollection<MasterDatum> MasterData { get; set; } = new List<MasterDatum>();

    public virtual ICollection<MonthlyReportColumn> MonthlyReportColumns { get; set; } = new List<MonthlyReportColumn>();

    public virtual ICollection<MonthlyReportEntry> MonthlyReportEntries { get; set; } = new List<MonthlyReportEntry>();

    public virtual ICollection<MonthlyReportMember> MonthlyReportMembers { get; set; } = new List<MonthlyReportMember>();

    public virtual ICollection<MonthlyReport> MonthlyReports { get; set; } = new List<MonthlyReport>();

    public virtual ICollection<OvertimeRequest> OvertimeRequests { get; set; } = new List<OvertimeRequest>();

    public virtual ICollection<PcvTemplateItem> PcvTemplateItems { get; set; } = new List<PcvTemplateItem>();

    public virtual ICollection<ProjectAssignmentPeriod> ProjectAssignmentPeriods { get; set; } = new List<ProjectAssignmentPeriod>();

    public virtual ICollection<ProjectAssignment> ProjectAssignments { get; set; } = new List<ProjectAssignment>();

    public virtual ICollection<ProjectChangeRequest> ProjectChangeRequests { get; set; } = new List<ProjectChangeRequest>();

    public virtual ICollection<ProjectContractStaff> ProjectContractStaffs { get; set; } = new List<ProjectContractStaff>();

    public virtual ICollection<ProjectCostCenter> ProjectCostCenters { get; set; } = new List<ProjectCostCenter>();

    public virtual ICollection<ProjectCssAnswer> ProjectCssAnswers { get; set; } = new List<ProjectCssAnswer>();

    public virtual ICollection<ProjectCssForm> ProjectCssForms { get; set; } = new List<ProjectCssForm>();

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

    public virtual ICollection<ProjectPcvResult> ProjectPcvResults { get; set; } = new List<ProjectPcvResult>();

    public virtual ICollection<ProjectQualityMetric> ProjectQualityMetrics { get; set; } = new List<ProjectQualityMetric>();

    public virtual ICollection<ProjectRisk> ProjectRisks { get; set; } = new List<ProjectRisk>();

    public virtual ICollection<ProjectStatusReport> ProjectStatusReports { get; set; } = new List<ProjectStatusReport>();

    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();

    public virtual ICollection<TenantIntegration> TenantIntegrations { get; set; } = new List<TenantIntegration>();

    public virtual ICollection<TenantRule> TenantRules { get; set; } = new List<TenantRule>();

    public virtual ICollection<TenantSetting> TenantSettings { get; set; } = new List<TenantSetting>();

    public virtual ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();

    public virtual ICollection<TimesheetEntry> TimesheetEntries { get; set; } = new List<TimesheetEntry>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
