namespace Dx.Domain.Constants;

/// <summary>
/// Permission keys (global catalog in table dx.permissions, seeded by V012).
/// Roles are per tenant; authorization never bypasses by role name (D3).
/// </summary>
public static class Permissions
{
    public const string TimesheetEdit = "timesheet.edit";
    public const string TimesheetTeam = "timesheet.team";
    public const string TimesheetAllView = "timesheet.all_view";
    public const string TimesheetAllEdit = "timesheet.all_edit";
    public const string TimesheetApprove = "timesheet.approve";
    public const string ProjectView = "project.view";
    public const string ProjectCreate = "project.create";
    public const string ProjectManage = "project.manage";
    public const string Staffing = "staffing";
    public const string EmployeeView = "employee.view";
    public const string EmployeeManage = "employee.manage";
    public const string EffortView = "effort.view";
    public const string EffortEdit = "effort.edit";
    public const string PcvManage = "pcv.manage";
    public const string CssManage = "css.manage";
    public const string ReportBasic = "report.basic";
    public const string ReportFinance = "report.finance";
    public const string ReportQuality = "report.quality";
    public const string AdminRoles = "admin.roles";
    public const string AdminSettings = "admin.settings";
    public const string AdminIntegrations = "admin.integrations";
    public const string AdminInsights = "admin.insights";
}
