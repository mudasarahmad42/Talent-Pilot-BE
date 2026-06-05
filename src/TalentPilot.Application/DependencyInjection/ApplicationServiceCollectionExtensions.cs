using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Ai;
using TalentPilot.Application.AiAssistant;
using TalentPilot.Application.Admin.AiSettings;
using TalentPilot.Application.Admin.AuditLogs;
using TalentPilot.Application.Admin.CandidateSources;
using TalentPilot.Application.Admin.Departments;
using TalentPilot.Application.Admin.Groups;
using TalentPilot.Application.Admin.HiringPipelines;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Application.Admin.Roles;
using TalentPilot.Application.Admin.Skills;
using TalentPilot.Application.Admin.TenantProfiles;
using TalentPilot.Application.Admin.Users;
using TalentPilot.Application.Admin.Workflows;
using TalentPilot.Application.Auth;
using TalentPilot.Application.Calendar;
using TalentPilot.Application.Notifications;
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
        services.AddScoped<IAdminDepartmentsService, AdminDepartmentsService>();
        services.AddScoped<IAdminGroupsService, AdminGroupsService>();
        services.AddScoped<IAdminRolesService, AdminRolesService>();
        services.AddScoped<IAdminSkillsService, AdminSkillsService>();
        services.AddScoped<IAdminNotificationsService, AdminNotificationsService>();
        services.AddScoped<IAdminAuditLogService, AdminAuditLogService>();
        services.AddScoped<IAdminAiSettingsService, AdminAiSettingsService>();
        services.AddScoped<IAdminCandidateSourcesService, AdminCandidateSourcesService>();
        services.AddScoped<IAdminWorkflowsService, AdminWorkflowsService>();
        services.AddScoped<IAdminHiringPipelinesService, AdminHiringPipelinesService>();
        services.AddScoped<IOperationsService, OperationsService>();
        services.AddScoped<IAiAssistantService, AiAssistantService>();
        services.AddScoped<IKnowledgeIndexingService, KnowledgeIndexingService>();
        services.AddScoped<IKnowledgeRetrievalService, KnowledgeRetrievalService>();
        services.AddScoped<IRagPromptBuilder, RagPromptBuilder>();
        services.AddSingleton<IApplicationDocumentTextExtractor, DocxApplicationDocumentTextExtractor>();
        services.AddScoped<IJobDescriptionDraftingAgent, JobDescriptionDraftingAgent>();
        services.AddScoped<ICvParserAgent, CvParserAgent>();
        services.AddScoped<IBenchMatchingAgent, BenchMatchingAgent>();
        services.AddScoped<ITalentRediscoveryAgent, TalentRediscoveryAgent>();
        services.AddScoped<IApplicantRankingAgent, ApplicantRankingAgent>();
        services.AddScoped<OnlineHeadhuntingBooleanQueryBuilder>();
        services.AddScoped<IOnlineHeadhuntingAgent, OnlineHeadhuntingAgent>();
        services.AddScoped<IInterviewQuestionRecommendationAgent, InterviewQuestionRecommendationAgent>();
        services.TryAddSingleton<ICalendarMeetingService, NoOpCalendarMeetingService>();
        services.TryAddSingleton<IRealtimeNotificationPublisher, NoOpRealtimeNotificationPublisher>();
        services.TryAddSingleton<IRealtimeConnectionCounter, NoOpRealtimeNotificationPublisher>();
        services.TryAddSingleton<IOnlineHeadhuntingJobQueue, NoOpOnlineHeadhuntingJobQueue>();

        return services;
    }
}
