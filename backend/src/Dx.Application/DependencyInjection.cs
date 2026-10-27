using Dx.Application.Access;
using Dx.Application.Ai;
using Dx.Application.Auth;
using Dx.Application.MasterData;
using Dx.Application.Organization;
using Dx.Application.Projects;
using Dx.Application.Settings;
using Dx.Application.Work;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dx.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AuthOptions>().Bind(config.GetSection(AuthOptions.Section));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<LoginThrottle>();

        services.AddScoped<AuthService>();
        services.AddScoped<MasterDataService>();
        services.AddScoped<RuleService>();
        services.AddScoped<SettingService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<HolidayService>();
        services.AddScoped<TemplateService>();
        services.AddScoped<ClientService>();
        services.AddScoped<DepartmentService>();
        services.AddScoped<EmployeeService>();
        services.AddScoped<RoleService>();
        services.AddScoped<AccountService>();
        services.AddScoped<ProjectAccess>();
        services.AddScoped<ProjectDetailService>();
        services.AddScoped<ProjectStaffingService>();
        services.AddScoped<ProjectTimelineService>();
        services.AddScoped<WorkScope>();
        services.AddScoped<TimesheetService>();
        services.AddScoped<RequestService>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<EffortService>();
        services.AddScoped<ProjectQualityFormsService>();
        services.AddScoped<ProjectAdminService>();
        services.AddScoped<MonthlyReportService>();
        services.AddScoped<AiService>();
        services.AddScoped<AiAdminService>();
        return services;
    }
}
