using Microsoft.AspNetCore.SignalR;
using TalentPilot.Application.Admin.Notifications;

namespace TalentPilot.Api.Realtime;

public sealed class SignalRNotificationRealtimePublisher : INotificationRealtimePublisher
{
    private const string NotificationReceivedClientMethod = "NotificationReceived";
    private readonly IHubContext<NotificationsHub> _hubContext;

    public SignalRNotificationRealtimePublisher(IHubContext<NotificationsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishToUserAsync(RealtimeNotificationPayload notification, CancellationToken cancellationToken)
    {
        var groupName = NotificationHubGroups.User(notification.TenantId, notification.RecipientUserId);
        return _hubContext.Clients.Group(groupName).SendAsync(
            NotificationReceivedClientMethod,
            notification,
            cancellationToken);
    }
}
