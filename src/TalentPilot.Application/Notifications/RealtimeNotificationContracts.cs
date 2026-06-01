namespace TalentPilot.Application.Notifications;

public sealed record RealtimeNotificationMessage(
    Guid NotificationId,
    Guid TenantId,
    Guid? RecipientUserId,
    string Title,
    string Message,
    string Category,
    string Severity,
    string? EntityType,
    string? EntityId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record RealtimeNotificationPublishResult(
    int ConnectedClientCount,
    DateTimeOffset SentAtUtc,
    IReadOnlyDictionary<Guid, Guid> RecipientNotificationIds);

public sealed record PersistedRealtimeNotification(Guid RecipientUserId, Guid NotificationId);

public interface IRealtimeNotificationPublisher
{
    Task<RealtimeNotificationPublishResult> PublishToTenantAsync(
        Guid tenantId,
        RealtimeNotificationMessage notification,
        CancellationToken cancellationToken);

    Task<RealtimeNotificationPublishResult> PublishToUserAsync(
        Guid tenantId,
        Guid userId,
        RealtimeNotificationMessage notification,
        CancellationToken cancellationToken);

    Task<RealtimeNotificationPublishResult> PublishToAllAsync(
        RealtimeNotificationMessage notification,
        CancellationToken cancellationToken);
}

public interface IRealtimeConnectionCounter
{
    int CountTenantConnections(Guid tenantId);
}

public interface IRealtimeNotificationRepository
{
    Task<IReadOnlyList<PersistedRealtimeNotification>> InsertForUsersAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> recipientUserIds,
        RealtimeNotificationMessage notification,
        CancellationToken cancellationToken);
}

public static class RealtimeNotificationGroups
{
    public static string Tenant(Guid tenantId) => $"tenant:{tenantId:D}";

    public static string User(Guid tenantId, Guid userId) => $"tenant:{tenantId:D}:user:{userId:D}";
}
