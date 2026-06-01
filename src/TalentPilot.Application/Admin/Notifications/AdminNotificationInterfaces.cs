using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Notifications;

public interface IAdminNotificationsService
{
    Task<Result<AdminNotificationEventsResponse>> ListEventsAsync(AdminNotificationEventsQuery query, CancellationToken cancellationToken);

    Task<Result<AdminNotificationEventDetails>> GetEventAsync(Guid eventId, CancellationToken cancellationToken);

    Task<Result<AdminNotificationTemplatesResponse>> ListTemplatesAsync(AdminNotificationTemplatesQuery query, CancellationToken cancellationToken);

    Task<Result<NotificationTemplateSummary>> UpdateTemplateAsync(Guid templateId, UpdateNotificationTemplateInput input, CancellationToken cancellationToken);

    Task<Result<SendTestNotificationEmailResponse>> SendTestEmailAsync(SendTestNotificationEmailInput input, CancellationToken cancellationToken);

    Task<Result<RealtimeNotificationConnectionStatusResponse>> GetRealtimeConnectionStatusAsync(CancellationToken cancellationToken);

    Task<Result<SendTestRealtimeNotificationResponse>> SendTestRealtimeNotificationAsync(CancellationToken cancellationToken);

    Task<Result> UpdateEventStatusAsync(Guid eventId, UpdateNotificationEventStatusInput input, CancellationToken cancellationToken);
}

public interface IAdminNotificationsRepository
{
    Task<AdminNotificationEventsResponse> ListEventsAsync(Guid tenantId, AdminNotificationEventsQuery query, CancellationToken cancellationToken);

    Task<AdminNotificationEventDetails?> GetEventAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken);

    Task<AdminNotificationTemplatesResponse> ListTemplatesAsync(Guid tenantId, AdminNotificationTemplatesQuery query, CancellationToken cancellationToken);

    Task<NotificationTemplateSummary?> GetTemplateAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken);

    Task UpdateTemplateAsync(Guid tenantId, Guid actorUserId, Guid templateId, UpdateNotificationTemplateInput input, string metadataJson, CancellationToken cancellationToken);

    Task RecordTestEmailSentAsync(Guid tenantId, Guid actorUserId, string recipientEmail, string providerMessageId, string metadataJson, CancellationToken cancellationToken);

    Task RecordRealtimeTestNotificationSentAsync(Guid tenantId, Guid actorUserId, Guid notificationId, int connectedClientCount, string metadataJson, CancellationToken cancellationToken);

    Task UpdateEventStatusAsync(Guid tenantId, Guid actorUserId, Guid eventId, string status, string metadataJson, CancellationToken cancellationToken);
}

public interface INotificationEmailSender
{
    Task<Result<NotificationEmailSendResult>> SendAsync(NotificationEmailMessage message, CancellationToken cancellationToken);
}

public interface INotificationOutboxProcessor
{
    Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken);
}
