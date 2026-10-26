using Dx.Application.Auth;
using Dx.Application.MasterData;
using Dx.Application.Projects;
using Dx.Application.Settings;
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
        return services;
    }
}
