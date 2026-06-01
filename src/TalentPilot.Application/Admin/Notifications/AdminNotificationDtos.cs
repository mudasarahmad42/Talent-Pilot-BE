namespace TalentPilot.Application.Admin.Notifications;

public sealed record AdminNotificationEventsQuery(string? Search, int Page, int PageSize);

public sealed record AdminNotificationTemplatesQuery(string? Search, int Page, int PageSize);

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

public sealed record AdminNotificationTemplatesResponse(
    AdminNotificationEventsSummary Summary,
    IReadOnlyList<NotificationTemplateSummary> Items,
    int Page,
    int PageSize,
    int TotalCount);

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

public sealed record SendTestNotificationEmailInput(string ToEmail);

public sealed record SendTestNotificationEmailResponse(
    string ToEmail,
    string Subject,
    string Provider,
    string MessageId,
    DateTimeOffset SubmittedAtUtc);

public sealed record SendTestRealtimeNotificationResponse(
    Guid NotificationId,
    string Title,
    string Message,
    int ConnectedClientCount,
    DateTimeOffset SentAtUtc);

public sealed record RealtimeNotificationConnectionStatusResponse(
    int ConnectedClientCount,
    DateTimeOffset CheckedAtUtc);

public sealed record NotificationEmailMessage(string ToEmail, string Subject, string TextBody, string HtmlBody);

public sealed record NotificationEmailSendResult(string Provider, string MessageId, DateTimeOffset SubmittedAtUtc);
