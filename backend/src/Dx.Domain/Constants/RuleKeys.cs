namespace Dx.Domain.Constants;

/// <summary>
/// Keys of dx.system_rules. The tenant value lives in dx.tenant_rules; a missing or
/// disabled row means "use the default" (D12).
/// </summary>
public static class RuleKeys
{
    public const string TimesheetFteHours = "TIMESHEET_FTE_HOURS";
    public const string ProjectEndingSoonDays = "PROJECT_ENDING_SOON_DAYS";
    public const string ProjectBudgetWarningThreshold = "PROJECT_BUDGET_WARNING_THRESHOLD";
}
