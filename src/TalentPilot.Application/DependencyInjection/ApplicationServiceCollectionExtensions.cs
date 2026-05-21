using Microsoft.Extensions.DependencyInjection;
using TalentPilot.Application.Admin.AiSettings;
using TalentPilot.Application.Admin.AuditLogs;
using TalentPilot.Application.Admin.Groups;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Application.Admin.Roles;
using TalentPilot.Application.Admin.TenantProfiles;
using TalentPilot.Application.Admin.Users;
using TalentPilot.Application.Auth;
using TalentPilot.Application.Operations;

namespace TalentPilot.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminTenantProfileService, AdminTenantProfileService>();
        services.AddScoped<IAdminUsersService, AdminUsersService>();
        services.AddScoped<IAdminAccessPoliciesService, AdminUsersService>();
        services.AddScoped<IAdminGroupsService, AdminGroupsService>();
        services.AddScoped<IAdminRolesService, AdminRolesService>();
        services.AddScoped<IAdminNotificationsService, AdminNotificationsService>();
        services.AddScoped<IAdminAuditLogService, AdminAuditLogService>();
        services.AddScoped<IAdminAiSettingsService, AdminAiSettingsService>();
        services.AddScoped<IOperationsService, OperationsService>();

        return services;
    }
}
