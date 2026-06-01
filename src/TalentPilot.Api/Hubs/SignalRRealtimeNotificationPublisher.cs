using Microsoft.AspNetCore.SignalR;
using TalentPilot.Application.Notifications;

namespace TalentPilot.Api.Hubs;

public sealed class SignalRRealtimeNotificationPublisher : IRealtimeNotificationPublisher
{
    private const string ClientMethodName = "NotificationReceived";

    private readonly IHubContext<NotificationsHub> _hubContext;
    private readonly RealtimeConnectionTracker _connectionTracker;
    private readonly IRealtimeNotificationRepository _notificationRepository;

    public SignalRRealtimeNotificationPublisher(
        IHubContext<NotificationsHub> hubContext,
        RealtimeConnectionTracker connectionTracker,
        IRealtimeNotificationRepository notificationRepository)
    {
        _hubContext = hubContext;
        _connectionTracker = connectionTracker;
        _notificationRepository = notificationRepository;
    }

    public async Task<RealtimeNotificationPublishResult> PublishToTenantAsync(
        Guid tenantId,
        RealtimeNotificationMessage notification,
        CancellationToken cancellationToken)
    {
        var connectedUserIds = _connectionTracker.TenantUserIds(tenantId);
        var persistedNotifications = await _notificationRepository.InsertForUsersAsync(
            tenantId,
            connectedUserIds,
            notification,
            cancellationToken);

        foreach (var persistedNotification in persistedNotifications)
        {
            var userNotification = notification with
            {
                NotificationId = persistedNotification.NotificationId,
                RecipientUserId = persistedNotification.RecipientUserId
            };

            await _hubContext.Clients
                .Group(RealtimeNotificationGroups.User(tenantId, persistedNotification.RecipientUserId))
                .SendAsync(ClientMethodName, userNotification, cancellationToken);
        }

        return new RealtimeNotificationPublishResult(
            _connectionTracker.CountTenantConnections(tenantId),
            DateTimeOffset.UtcNow,
            persistedNotifications.ToDictionary(
                notification => notification.RecipientUserId,
                notification => notification.NotificationId));
    }

    public async Task<RealtimeNotificationPublishResult> PublishToUserAsync(
        Guid tenantId,
        Guid userId,
        RealtimeNotificationMessage notification,
        CancellationToken cancellationToken)
    {
        var persistedNotifications = await _notificationRepository.InsertForUsersAsync(
            tenantId,
            [userId],
            notification,
            cancellationToken);
        var notificationId = persistedNotifications.FirstOrDefault()?.NotificationId ?? notification.NotificationId;
        var userNotification = notification with
        {
            NotificationId = notificationId,
            RecipientUserId = userId
        };

        await _hubContext.Clients
            .Group(RealtimeNotificationGroups.User(tenantId, userId))
            .SendAsync(ClientMethodName, userNotification, cancellationToken);

        return new RealtimeNotificationPublishResult(
            _connectionTracker.CountUserConnections(tenantId, userId),
            DateTimeOffset.UtcNow,
            persistedNotifications.ToDictionary(
                notification => notification.RecipientUserId,
                notification => notification.NotificationId));
    }

    public async Task<RealtimeNotificationPublishResult> PublishToAllAsync(
        RealtimeNotificationMessage notification,
        CancellationToken cancellationToken)
    {
        await _hubContext.Clients.All.SendAsync(ClientMethodName, notification, cancellationToken);

        return new RealtimeNotificationPublishResult(
            _connectionTracker.CountAllConnections(),
            DateTimeOffset.UtcNow,
            new Dictionary<Guid, Guid>());
    }
}
