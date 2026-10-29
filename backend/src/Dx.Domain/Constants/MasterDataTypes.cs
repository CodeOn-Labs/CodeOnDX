namespace Dx.Domain.Constants;

/// <summary>Values of dx.master_data.type (tenant-editable lookups, Admin › Categories).</summary>
public static class MasterDataTypes
{
    public const string ProjectStatus = "project_status";
    public const string ProjectLevel = "project_level";
    public const string RiskLevel = "risk_level";
    public const string HrLevel = "hr_level";
    public const string JobPosition = "job_position";
    public const string TaskType = "task_type";
    public const string EmployeeStatus = "employee_status";
    public const string LeaveType = "leave_type";
    public const string LinkType = "link_type";
    public const string Country = "country";
    public const string Industry = "industry";
    public const string Currency = "currency";
    public const string PerformanceLevel = "performance_level";
    /// <summary>Technical request type of a Jira issue — issue logwork is summed per type for cost allocation.</summary>
    public const string TechRequestType = "tech_request_type";
}

/// <summary>Codes the application logic relies on (system rows of master_data).</summary>
public static class MasterDataCodes
{
    public const string ProjectStatusRunning = "RUNNING";
    public const string ProjectStatusEnding = "ENDING";
    public const string ProjectStatusPlanning = "PLANNING";
    public const string ProjectStatusCompleted = "COMPLETED";
    public const string RiskLevelHigh = "HIGH";
    public const string EmployeeStatusActive = "ACTIVE";
    public const string EmployeeStatusResigned = "RESIGNED";
}
