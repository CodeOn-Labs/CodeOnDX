using Dx.Application.Auth;
using Dx.Application.Common;
using Dx.Infrastructure.Database;
using Dx.Infrastructure.Persistence;
using Dx.Infrastructure.Security;
using Dx.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dx.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Dx")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Dx");

        services.AddDbContext<DxDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<DxDbContext>());

        services.AddOptions<DatabaseOptions>().Bind(config.GetSection(DatabaseOptions.Section));
        services.AddTransient<DatabaseMigrator>();

        services.AddOptions<JwtOptions>().Bind(config.GetSection(JwtOptions.Section))
            .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.SigningKey) >= 32, "Jwt:SigningKey must be at least 32 bytes (put it in appsettings.Secrets.json)")
            .ValidateOnStart();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<ITenantClock, TenantClock>();
        return services;
    }
}
