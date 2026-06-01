namespace TalentPilot.Application.Notifications;

public sealed class NoOpRealtimeNotificationPublisher : IRealtimeNotificationPublisher, IRealtimeConnectionCounter
{
    public Task<RealtimeNotificationPublishResult> PublishToTenantAsync(
        Guid tenantId,
        RealtimeNotificationMessage notification,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new RealtimeNotificationPublishResult(
            0,
            DateTimeOffset.UtcNow,
            new Dictionary<Guid, Guid>()));
    }

    public Task<RealtimeNotificationPublishResult> PublishToUserAsync(
        Guid tenantId,
        Guid userId,
        RealtimeNotificationMessage notification,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new RealtimeNotificationPublishResult(
            0,
            DateTimeOffset.UtcNow,
            new Dictionary<Guid, Guid>()));
    }

    public Task<RealtimeNotificationPublishResult> PublishToAllAsync(
        RealtimeNotificationMessage notification,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new RealtimeNotificationPublishResult(
            0,
            DateTimeOffset.UtcNow,
            new Dictionary<Guid, Guid>()));
    }

    public int CountTenantConnections(Guid tenantId)
    {
        return 0;
    }
}
