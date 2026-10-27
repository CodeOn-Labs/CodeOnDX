namespace Dx.Domain.Constants;

/// <summary>
/// Keys of dx.system_rules. The tenant value lives in dx.tenant_rules; a missing or
/// disabled row means "use the default" (D12).
/// </summary>
public static class RuleKeys
{
    public const string TimesheetFteHours = "TIMESHEET_FTE_HOURS";
    public const string TimesheetMinHours = "TIMESHEET_MIN_HOURS";
    public const string TimesheetMaxHours = "TIMESHEET_MAX_HOURS";
    public const string TimesheetWeeklyMaxHours = "TIMESHEET_WEEKLY_MAX_HOURS";
    public const string TimesheetEditLimitDays = "TIMESHEET_EDIT_LIMIT_DAYS";
    public const string TimesheetLockDateOfMonth = "TIMESHEET_LOCK_DATE_OF_MONTH";
    public const string TimesheetAllowFuture = "TIMESHEET_ALLOW_FUTURE";
    public const string ProjectEndingSoonDays = "PROJECT_ENDING_SOON_DAYS";
    public const string ProjectBudgetWarningThreshold = "PROJECT_BUDGET_WARNING_THRESHOLD";
    public const string HrOverloadAllocationThreshold = "HR_OVERLOAD_ALLOCATION_THRESHOLD";
    public const string HrMinAllocationThreshold = "HR_MIN_ALLOCATION_THRESHOLD";
    public const string HrUtilizationTargetPercent = "HR_UTILIZATION_TARGET_PERCENT";
    public const string LeaveAnnualDays = "LEAVE_ANNUAL_DAYS";
    public const string LeaveSeniorityStepYears = "LEAVE_SENIORITY_STEP_YEARS";
    public const string LeaveCarryOverMaxDays = "LEAVE_CARRY_OVER_MAX_DAYS";
    public const string LeaveMinNoticeDays = "LEAVE_MIN_NOTICE_DAYS";
    public const string OtMaxHoursPerDay = "OT_MAX_HOURS_PER_DAY";
    public const string OtMaxHoursPerMonth = "OT_MAX_HOURS_PER_MONTH";
    public const string OtMaxHoursPerYear = "OT_MAX_HOURS_PER_YEAR";
    public const string AttendanceLateGraceMinutes = "ATTENDANCE_LATE_GRACE_MINUTES";
    public const string AttendanceEditLimitDays = "ATTENDANCE_EDIT_LIMIT_DAYS";
}
