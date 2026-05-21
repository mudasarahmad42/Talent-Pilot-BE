using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Notifications;

public interface IAdminNotificationsService
{
    Task<Result<AdminNotificationEventsResponse>> ListEventsAsync(AdminNotificationEventsQuery query, CancellationToken cancellationToken);

    Task<Result<AdminNotificationEventDetails>> GetEventAsync(Guid eventId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<NotificationTemplateSummary>>> ListTemplatesAsync(CancellationToken cancellationToken);

    Task<Result<NotificationTemplateSummary>> UpdateTemplateAsync(Guid templateId, UpdateNotificationTemplateInput input, CancellationToken cancellationToken);

    Task<Result> UpdateEventStatusAsync(Guid eventId, UpdateNotificationEventStatusInput input, CancellationToken cancellationToken);
}

public interface IAdminNotificationsRepository
{
    Task<AdminNotificationEventsResponse> ListEventsAsync(Guid tenantId, AdminNotificationEventsQuery query, CancellationToken cancellationToken);

    Task<AdminNotificationEventDetails?> GetEventAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationTemplateSummary>> ListTemplatesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<NotificationTemplateSummary?> GetTemplateAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken);

    Task UpdateTemplateAsync(Guid tenantId, Guid actorUserId, Guid templateId, UpdateNotificationTemplateInput input, string metadataJson, CancellationToken cancellationToken);

    Task UpdateEventStatusAsync(Guid tenantId, Guid actorUserId, Guid eventId, string status, string metadataJson, CancellationToken cancellationToken);
}

public interface INotificationOutboxProcessor
{
    Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken);
}
