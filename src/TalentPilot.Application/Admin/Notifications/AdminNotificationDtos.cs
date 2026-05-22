namespace TalentPilot.Application.Admin.Notifications;

public sealed record AdminNotificationEventsQuery(string? Search, int Page, int PageSize);

public sealed record AdminNotificationEventsResponse(
    AdminNotificationEventsSummary Summary,
    IReadOnlyList<AdminNotificationEventListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminNotificationEventsSummary(
    int ActiveEventCount,
    int EditableTemplateCount,
    int PendingOutboxCount,
    int FailedOutboxCount);

public sealed record AdminNotificationEventListItem(
    Guid EventId,
    string EventCode,
    string Name,
    string Recipient,
    string TemplateName,
    string LifecycleStatus,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminNotificationEventDetails(
    Guid EventId,
    string EventCode,
    string Name,
    string Recipient,
    string LifecycleStatus,
    IReadOnlyList<NotificationTemplateSummary> Templates);

public sealed record NotificationTemplateSummary(
    Guid TemplateId,
    string EventCode,
    string Name,
    string Recipient,
    string Subject,
    string Body,
    IReadOnlyList<string> Variables,
    string LifecycleStatus,
    DateTimeOffset UpdatedAtUtc,
    Guid UpdatedByUserId);

public sealed record UpdateNotificationTemplateInput(string Subject, string Body);

public sealed record UpdateNotificationEventStatusInput(string Status);

public sealed record RealtimeNotificationPayload(
    Guid Id,
    Guid TenantId,
    Guid RecipientUserId,
    string Title,
    string Message,
    string EntityType,
    Guid EntityId,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt,
    string EventCode);

public sealed record AdminTestNotificationResponse(
    string HubPath,
    string ClientMethod,
    string Channel,
    string DeliveryStatus,
    Guid OutboxId,
    RealtimeNotificationPayload Notification);

public sealed record QueuedAdminTestNotification(
    Guid OutboxId,
    RealtimeNotificationPayload Notification);
