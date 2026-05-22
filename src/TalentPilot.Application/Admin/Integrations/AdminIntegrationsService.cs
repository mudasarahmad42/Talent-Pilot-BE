using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Integrations;

public sealed class AdminIntegrationsService : IAdminIntegrationsService
{
    private readonly IAdminNotificationsRepository _notificationsRepository;
    private readonly IAdminRuntimeSettings _runtimeSettings;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminIntegrationsService(
        IAdminNotificationsRepository notificationsRepository,
        IAdminRuntimeSettings runtimeSettings,
        ICurrentUserAccessor currentUser)
    {
        _notificationsRepository = notificationsRepository;
        _runtimeSettings = runtimeSettings;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminIntegrationStatusResponse>> GetStatusAsync(CancellationToken cancellationToken)
    {
        var notificationSummary = (await _notificationsRepository.ListEventsAsync(
            _currentUser.TenantId,
            new AdminNotificationEventsQuery(Search: null, Page: 1, PageSize: 1),
            cancellationToken)).Summary;

        var items = new[]
        {
            BuildEmailOutboxStatus(notificationSummary),
            BuildSignalRStatus(notificationSummary),
            new AdminIntegrationStatusItem(
                "candidate-portal",
                "Candidate Portal",
                "Candidate Experience",
                "Contracted",
                Enabled: true,
                Editable: false,
                "Talent Pilot portal",
                "Portal application workflow",
                "Candidates apply through the Talent Pilot candidate portal. External job board application capture is outside MVP.",
                []),
            new AdminIntegrationStatusItem(
                "linkedin-mock-publishing",
                "LinkedIn Mock Publishing",
                "Job Publishing",
                "Mock Only",
                Enabled: false,
                Editable: false,
                "MVP mock contract",
                "No external automation",
                "LinkedIn publishing can be represented as a mock status only. Paid automation and manual copy-paste publishing are not product capabilities.",
                []),
            new AdminIntegrationStatusItem(
                "docx-resume-parser",
                "DOCX Resume Parser",
                "Resume Intake",
                "Contracted",
                Enabled: true,
                Editable: false,
                "Local backend parser",
                "DOCX upload through candidate portal",
                "MVP accepts DOCX resumes and keeps parsing in the backend with a local/free library boundary.",
                []),
            new AdminIntegrationStatusItem(
                "ai-runtime",
                "Ollama/Mock AI Runtime",
                "AI Runtime",
                "Available",
                Enabled: true,
                Editable: false,
                $"{_runtimeSettings.Provider}; LLM {_runtimeSettings.LlmModel}; embeddings {_runtimeSettings.EmbeddingModel}",
                "Backend AI abstractions",
                "AI runtime stays local/free by default. Recommendations are advisory and never make final hiring decisions.",
                [new AdminIntegrationMetric("EmbeddingDimensions", _runtimeSettings.EmbeddingDimensions)])
        };

        return Result<AdminIntegrationStatusResponse>.Success(new AdminIntegrationStatusResponse(
            ReadOnly: true,
            TotalCount: items.Length,
            Items: items));
    }

    private static AdminIntegrationStatusItem BuildEmailOutboxStatus(AdminNotificationEventsSummary summary)
    {
        var status = summary.FailedOutboxCount > 0
            ? "Degraded"
            : "Available";

        return new AdminIntegrationStatusItem(
            "email-outbox",
            "Email Outbox",
            "Notifications",
            status,
            Enabled: true,
            Editable: false,
            "SQL outbox with local worker",
            "Email channel",
            "Backend owns email notification delivery through outbox records and worker processing. Admins manage event/template text, not delivery providers.",
            [
                new AdminIntegrationMetric("PendingOutbox", summary.PendingOutboxCount),
                new AdminIntegrationMetric("FailedOutbox", summary.FailedOutboxCount)
            ]);
    }

    private static AdminIntegrationStatusItem BuildSignalRStatus(AdminNotificationEventsSummary summary)
    {
        return new AdminIntegrationStatusItem(
            "signalr-in-app",
            "SignalR/In-App Notifications",
            "Notifications",
            "In-App Available",
            Enabled: true,
            Editable: false,
            "Backend notification records; SignalR transport boundary",
            "In-app realtime channel",
            "In-app notifications are backend-owned. SignalR is the realtime delivery path and remains separate from email outbox delivery.",
            [new AdminIntegrationMetric("ActiveNotificationEvents", summary.ActiveEventCount)]);
    }
}
