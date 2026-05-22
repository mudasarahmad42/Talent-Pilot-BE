namespace TalentPilot.Application.Admin.Notifications;

internal sealed class NoOpNotificationRealtimePublisher : INotificationRealtimePublisher
{
    public Task PublishToUserAsync(RealtimeNotificationPayload notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
