using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Abstractions;
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
using TalentPilot.Application.AiAssistant;
using TalentPilot.Application.Calendar;
using TalentPilot.Application.Documents;
using TalentPilot.Application.Notifications;
using TalentPilot.Application.Operations;
using TalentPilot.Infrastructure.Ai;
using TalentPilot.Common.Time;
using TalentPilot.Infrastructure.Auth;
using TalentPilot.Infrastructure.Calendar;
using TalentPilot.Infrastructure.Documents;
using TalentPilot.Infrastructure.Notifications;
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
            AccessTokenMinutes = int.TryParse(configuration["Auth:AccessTokenMinutes"], out var accessTokenMinutes)
                ? accessTokenMinutes
                : 60,
            RefreshTokenDays = int.TryParse(configuration["Auth:RefreshTokenDays"], out var refreshTokenDays)
                ? refreshTokenDays
                : 7
        });

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAdminRuntimeSettings, AdminRuntimeSettings>();
        services.AddSingleton<IDocumentExportService, OpenXmlDocumentExportService>();
        services.AddSingleton<IOptions<LocalApplicationDocumentStorageOptions>>(Options.Create(new LocalApplicationDocumentStorageOptions
        {
            RootPath = configuration["ApplicationDocuments:LocalRootPath"] ?? string.Empty,
            StorageContainer = configuration["ApplicationDocuments:StorageContainer"] ?? "application-documents"
        }));
        services.AddSingleton<IApplicationDocumentStorage, LocalApplicationDocumentStorage>();
        services.AddSingleton<IOptions<GoogleCalendarOptions>>(Options.Create(BuildGoogleCalendarOptions(configuration)));
        services.AddSingleton<IGoogleCalendarTokenProtector, GoogleCalendarTokenProtector>();
        services.AddScoped<IGoogleCalendarService, GoogleCalendarMeetingService>();
        services.AddScoped<ICalendarMeetingService, GoogleCalendarMeetingService>();
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IAiRuntimeSettingsResolver, DapperAiRuntimeSettingsResolver>();
        services.AddSingleton<IAiAgentRunLogger, DapperAiAgentRunLogger>();
        services.AddSingleton<IVectorStore, DapperVectorStore>();
        services.AddSingleton(_ => new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        });
        services.AddScoped<IAiModelProvider, OllamaAiModelProvider>();
        services.AddScoped<IAiModelHealthChecker, OllamaAiModelHealthChecker>();
        services.AddScoped<IEmbeddingProvider, OllamaEmbeddingProvider>();
        services.AddScoped<ISemanticSimilarityHealthChecker, SemanticSimilarityHealthChecker>();
        services.AddSingleton<IOptions<GoogleCustomSearchOptions>>(Options.Create(BuildGoogleCustomSearchOptions(configuration)));
        services.AddSingleton<IOptions<TavilySearchOptions>>(Options.Create(BuildTavilySearchOptions(configuration)));
        services.AddSingleton<IOptions<GitHubCandidateSearchOptions>>(Options.Create(BuildGitHubCandidateSearchOptions(configuration)));
        services.AddSingleton<IWebResearchProvider>(provider =>
        {
            var configuredProvider = configuration["WebResearch:Provider"] ?? "Tavily";
            if (string.Equals(configuredProvider, "GoogleCustomSearch", StringComparison.OrdinalIgnoreCase))
            {
                return ActivatorUtilities.CreateInstance<GoogleCustomSearchWebResearchProvider>(provider);
            }

            return ActivatorUtilities.CreateInstance<TavilySearchWebResearchProvider>(provider);
        });
        services.AddSingleton<IGitHubCandidateSearchProvider, GitHubCandidateSearchProvider>();
        services.AddNotificationEmailSenderServices(configuration);

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
            services.AddSingleton<IKnowledgeRepository, DapperKnowledgeRepository>();
            services.AddSingleton<DapperAdminCenterRepository>();
            services.AddSingleton<IAdminUsersRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminAccessPoliciesRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminDepartmentsRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminGroupsRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminRolesRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminSkillsRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminNotificationsRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminAuditLogRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminAiSettingsRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminCandidateSourcesRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminWorkflowsRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IAdminHiringPipelinesRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<IRealtimeNotificationRepository>(provider => provider.GetRequiredService<DapperAdminCenterRepository>());
            services.AddSingleton<INotificationEmailProviderSettingsResolver, DapperNotificationEmailProviderSettingsResolver>();
            services.AddSingleton<INotificationOutboxProcessor, DapperNotificationOutboxProcessor>();
            services.AddSingleton<INotificationWorkerStatusStore, DapperNotificationWorkerStatusStore>();
            services.AddSingleton<IWebResearchQuotaStore, DapperWebResearchQuotaStore>();
            services.AddSingleton<IGoogleCalendarConnectionRepository, DapperGoogleCalendarConnectionRepository>();
        }
        else
        {
            services.AddSingleton<IAdminTenantProfileRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IOperationsRepository, DapperOperationsRepository>();
            services.AddSingleton<IKnowledgeRepository, DapperKnowledgeRepository>();
            services.AddSingleton<IAdminUsersRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminAccessPoliciesRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminDepartmentsRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminGroupsRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminRolesRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminSkillsRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminNotificationsRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminAuditLogRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminAiSettingsRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminCandidateSourcesRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminWorkflowsRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IAdminHiringPipelinesRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IRealtimeNotificationRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<INotificationEmailProviderSettingsResolver>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<INotificationOutboxProcessor>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<INotificationWorkerStatusStore>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
            services.AddSingleton<IWebResearchQuotaStore, InMemoryWebResearchQuotaStore>();
            services.AddSingleton<IGoogleCalendarConnectionRepository>(provider => provider.GetRequiredService<InMemoryTalentPilotRepository>());
        }

        return services;
    }

    public static IServiceCollection AddNotificationEmailSenderServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IOptions<ResendEmailOptions>>(Options.Create(BuildResendEmailOptions(configuration)));
        services.AddSingleton<IOptions<MicrosoftGraphEmailOptions>>(Options.Create(BuildMicrosoftGraphEmailOptions(configuration)));
        services.AddSingleton<INotificationEmailProviderSender, ResendNotificationEmailSender>();
        services.AddSingleton<INotificationEmailProviderSender, MicrosoftGraphNotificationEmailSender>();
        services.AddSingleton<INotificationEmailSender, ConfiguredNotificationEmailSender>();

        return services;
    }

    private static GoogleCustomSearchOptions BuildGoogleCustomSearchOptions(IConfiguration configuration)
    {
        return new GoogleCustomSearchOptions
        {
            Enabled = !bool.TryParse(configuration["GoogleSearch:Enabled"], out var enabled) || enabled,
            ApiKey = configuration["GoogleSearch:ApiKey"]
                ?? Environment.GetEnvironmentVariable("GOOGLE_SEARCH_API_KEY")
                ?? string.Empty,
            SearchEngineId = configuration["GoogleSearch:SearchEngineId"]
                ?? configuration["GoogleSearch:Cx"]
                ?? Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID")
                ?? Environment.GetEnvironmentVariable("GOOGLE_SEARCH_CX")
                ?? string.Empty,
            BaseUrl = configuration["GoogleSearch:BaseUrl"] ?? "https://www.googleapis.com/customsearch/v1",
            DailyRequestLimit = int.TryParse(configuration["GoogleSearch:DailyRequestLimit"], out var limit)
                ? Math.Clamp(limit, 0, 60)
                : 60,
            RequestTimeoutSeconds = int.TryParse(configuration["GoogleSearch:RequestTimeoutSeconds"], out var timeout)
                ? Math.Clamp(timeout, 5, 60)
                : 15
        };
    }

    private static GoogleCalendarOptions BuildGoogleCalendarOptions(IConfiguration configuration)
    {
        return new GoogleCalendarOptions
        {
            Enabled = bool.TryParse(configuration["GoogleCalendar:Enabled"], out var enabled) && enabled,
            ApplicationName = configuration["GoogleCalendar:ApplicationName"] ?? "Talent Pilot",
            CalendarId = configuration["GoogleCalendar:CalendarId"] ?? "primary",
            DefaultTimeZoneId = configuration["GoogleCalendar:DefaultTimeZoneId"]
                ?? configuration["GoogleCalendar:TimeZone"]
                ?? "UTC",
            CreateGoogleMeetLink = !bool.TryParse(configuration["GoogleCalendar:CreateGoogleMeetLink"], out var createMeetLink)
                || createMeetLink,
            ClientId = FirstConfigured(
                configuration["GoogleCalendar:ClientId"],
                Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_CLIENT_ID")),
            ClientSecret = FirstConfigured(
                configuration["GoogleCalendar:ClientSecret"],
                Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_CLIENT_SECRET")),
            RedirectUri = FirstConfigured(
                configuration["GoogleCalendar:RedirectUri"],
                Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_REDIRECT_URI")),
            Scope = FirstConfigured(
                configuration["GoogleCalendar:Scope"],
                Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_SCOPE"),
                "https://www.googleapis.com/auth/calendar.events"),
            FrontendSuccessRedirectUrl = FirstConfigured(
                configuration["GoogleCalendar:FrontendSuccessRedirectUrl"],
                Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_FRONTEND_SUCCESS_URL"),
                "/settings/integrations/google-calendar/success"),
            FrontendErrorRedirectUrl = FirstConfigured(
                configuration["GoogleCalendar:FrontendErrorRedirectUrl"],
                Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_FRONTEND_ERROR_URL"),
                "/settings/integrations/google-calendar/error"),
            TokenProtectionKey = FirstConfigured(
                configuration["GoogleCalendar:TokenProtectionKey"],
                Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_TOKEN_PROTECTION_KEY"),
                configuration["Jwt:SigningKey"])
        };
    }

    private static TavilySearchOptions BuildTavilySearchOptions(IConfiguration configuration)
    {
        return new TavilySearchOptions
        {
            Enabled = !bool.TryParse(configuration["TavilySearch:Enabled"], out var enabled) || enabled,
            ApiKey = configuration["TavilySearch:ApiKey"]
                ?? Environment.GetEnvironmentVariable("TAVILY_API_KEY")
                ?? Environment.GetEnvironmentVariable("TAVILY_SEARCH_API_KEY")
                ?? string.Empty,
            BaseUrl = configuration["TavilySearch:BaseUrl"] ?? "https://api.tavily.com/search",
            DailyRequestLimit = int.TryParse(configuration["TavilySearch:DailyRequestLimit"], out var limit)
                ? Math.Clamp(limit, 0, 60)
                : 60,
            RequestTimeoutSeconds = int.TryParse(configuration["TavilySearch:RequestTimeoutSeconds"], out var timeout)
                ? Math.Clamp(timeout, 5, 60)
                : 20,
            SearchDepth = NormalizeTavilySearchDepth(configuration["TavilySearch:SearchDepth"] ?? "basic")
        };
    }

    private static GitHubCandidateSearchOptions BuildGitHubCandidateSearchOptions(IConfiguration configuration)
    {
        return new GitHubCandidateSearchOptions
        {
            Enabled = !bool.TryParse(configuration["GitHubSearch:Enabled"], out var enabled) || enabled,
            ApiBaseUrl = configuration["GitHubSearch:ApiBaseUrl"] ?? "https://api.github.com",
            Token = configuration["GitHubSearch:Token"]
                ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN")
                ?? Environment.GetEnvironmentVariable("GITHUB_API_TOKEN")
                ?? string.Empty,
            RequestTimeoutSeconds = int.TryParse(configuration["GitHubSearch:RequestTimeoutSeconds"], out var timeout)
                ? Math.Clamp(timeout, 5, 60)
                : 20
        };
    }

    private static ResendEmailOptions BuildResendEmailOptions(IConfiguration configuration)
    {
        return new ResendEmailOptions
        {
            ApiKey = configuration["Resend:ApiKey"]
                ?? Environment.GetEnvironmentVariable("RESEND_APITOKEN")
                ?? Environment.GetEnvironmentVariable("RESEND_API_KEY")
                ?? string.Empty,
            FromEmail = configuration["Resend:FromEmail"] ?? "onboarding@resend.dev"
        };
    }

    private static MicrosoftGraphEmailOptions BuildMicrosoftGraphEmailOptions(IConfiguration configuration)
    {
        return new MicrosoftGraphEmailOptions
        {
            TenantId = FirstConfigured(
                configuration["MicrosoftGraphEmail:TenantId"],
                Environment.GetEnvironmentVariable("MICROSOFT_GRAPH_TENANT_ID"),
                Environment.GetEnvironmentVariable("AZURE_TENANT_ID")),
            ClientId = FirstConfigured(
                configuration["MicrosoftGraphEmail:ClientId"],
                Environment.GetEnvironmentVariable("MICROSOFT_GRAPH_CLIENT_ID"),
                Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")),
            ClientSecret = FirstConfigured(
                configuration["MicrosoftGraphEmail:ClientSecret"],
                Environment.GetEnvironmentVariable("MICROSOFT_GRAPH_CLIENT_SECRET"),
                Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET")),
            FromEmail = FirstConfigured(
                configuration["MicrosoftGraphEmail:FromEmail"],
                Environment.GetEnvironmentVariable("MICROSOFT_GRAPH_FROM_EMAIL")),
            SaveToSentItems = !bool.TryParse(configuration["MicrosoftGraphEmail:SaveToSentItems"], out var saveToSentItems)
                || saveToSentItems
        };
    }

    private static string FirstConfigured(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string NormalizeTavilySearchDepth(string searchDepth)
    {
        return searchDepth.Trim().ToLowerInvariant() switch
        {
            "advanced" => "advanced",
            "fast" => "fast",
            "ultra-fast" => "ultra-fast",
            _ => "basic"
        };
    }

    private static bool UsesSqlServerIdentity(IConfiguration configuration)
    {
        var provider = configuration["DataAccess:IdentityProvider"]
            ?? configuration["DataAccess:Provider"]
            ?? "InMemory";

        return string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase);
    }
}
