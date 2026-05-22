using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Admin.AuditLogs;
using TalentPilot.Application.Admin.Groups;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Application.Admin.Roles;
using TalentPilot.Application.Admin.TenantProfiles;
using TalentPilot.Application.Admin.Users;
using TalentPilot.Application.Auth;
using TalentPilot.Application.Operations;
using TalentPilot.Common.Time;
using TalentPilot.Infrastructure.Auth;
using TalentPilot.Infrastructure.Persistence;
using TalentPilot.Infrastructure.Persistence.Repositories;
using TalentPilot.Infrastructure.Runtime;

namespace TalentPilot.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IOptions<JwtOptions>>(Options.Create(new JwtOptions
        {
            Issuer = configuration["Jwt:Issuer"] ?? "TalentPilot",
            Audience = configuration["Jwt:Audience"] ?? "TalentPilot.Web",
            SigningKey = configuration["Jwt:SigningKey"] ?? "development-only-change-this-key-before-production-32"
        }));

        services.AddSingleton(new AuthRuntimeOptions
        {
            AllowDemoCardLogin = bool.TryParse(configuration["Auth:AllowDemoCardLogin"], out var allowDemo) && allowDemo,
            AccessTokenMinutes = int.TryParse(configuration["Auth:AccessTokenMinutes"], out var accessTokenMinutes)
                ? accessTokenMinutes
                : 60,
            RefreshTokenDays = int.TryParse(configuration["Auth:RefreshTokenDays"], out var refreshTokenDays)
                ? refreshTokenDays
                : 7
        });

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAdminRuntimeSettings, AdminRuntimeSettings>();
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();

        services.AddSingleton<IPasswordVerifier, BCryptPasswordVerifier>();
        services.AddSingleton<ITokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.AddSingleton<InMemoryTalentPilotRepository>();
        if (UsesSqlServerIdentity(configuration))
        {
            services.AddSingleton<IIdentityRepository, DapperIdentityRepository>();
        }
        else
        {
            services.AddSingleton<IIdentityRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
        }

        if (UsesSqlServerIdentity(configuration))
        {
            services.AddSingleton<IAdminTenantProfileRepository, DapperAdminTenantProfileRepository>();
            services.AddSingleton<IOperationsRepository, DapperOperationsRepository>();
            services.AddSingleton<DapperAdminCenterRepository>();
            services.AddSingleton<IAdminUsersRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminAccessPoliciesRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminGroupsRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminRolesRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminNotificationsRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminAuditLogRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<INotificationOutboxProcessor>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
        }
        else
        {
            services.AddSingleton<IAdminTenantProfileRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IOperationsRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminUsersRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminAccessPoliciesRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminGroupsRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminRolesRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminNotificationsRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminAuditLogRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<INotificationOutboxProcessor>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
        }

        return services;
    }

    private static bool UsesSqlServerIdentity(IConfiguration configuration)
    {
        var provider = configuration["DataAccess:IdentityProvider"]
            ?? configuration["DataAccess:Provider"]
            ?? "InMemory";

        return string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase);
    }
}
