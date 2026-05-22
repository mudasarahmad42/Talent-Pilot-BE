using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Domain.Notifications;

namespace TalentPilot.Tests.Application;

public sealed class AdminNotificationsServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task SendTestNotificationAsync_QueuesPublishesAndMarksOutboxSent()
    {
        var repository = new FakeNotificationsRepository();
        var publisher = new FakeRealtimePublisher();
        var service = new AdminNotificationsService(repository, new FakeCurrentUser(), publisher);

        var result = await service.SendTestNotificationAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(repository.QueueCalled);
        Assert.Equal(TenantId, repository.LastTenantId);
        Assert.Equal(UserId, repository.LastActorUserId);
        Assert.Equal("admin@tkxel.com", repository.LastActorEmail);
        Assert.Equal(repository.Queued.OutboxId, result.Value.OutboxId);
        Assert.Equal("/hubs/notifications", result.Value.HubPath);
        Assert.Equal("NotificationReceived", result.Value.ClientMethod);
        Assert.Equal(NotificationChannels.SignalR, result.Value.Channel);
        Assert.Equal("Sent", result.Value.DeliveryStatus);
        Assert.Equal(repository.Queued.Notification, publisher.PublishedNotification);
        Assert.Contains(repository.StatusUpdates, update => update.OutboxId == repository.Queued.OutboxId && update.Status == "Sent");
    }

    [Fact]
    public async Task SendTestNotificationAsync_WhenRealtimePublishFails_MarksOutboxFailed()
    {
        var repository = new FakeNotificationsRepository();
        var publisher = new FakeRealtimePublisher { ExceptionToThrow = new InvalidOperationException("hub unavailable") };
        var service = new AdminNotificationsService(repository, new FakeCurrentUser(), publisher);

        var result = await service.SendTestNotificationAsync(CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("notifications.test_delivery_failed", result.Error.Code);
        var update = Assert.Single(repository.StatusUpdates);
        Assert.Equal(repository.Queued.OutboxId, update.OutboxId);
        Assert.Equal("Failed", update.Status);
        Assert.Equal("hub unavailable", update.LastError);
    }

    private sealed class FakeCurrentUser : ICurrentUserAccessor
    {
        public Guid UserId => AdminNotificationsServiceTests.UserId;

        public Guid TenantId => AdminNotificationsServiceTests.TenantId;

        public string Email => "admin@tkxel.com";

        public IReadOnlySet<string> RoleCodes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FakeRealtimePublisher : INotificationRealtimePublisher
    {
        public Exception? ExceptionToThrow { get; init; }

        public RealtimeNotificationPayload? PublishedNotification { get; private set; }

        public Task PublishToUserAsync(RealtimeNotificationPayload notification, CancellationToken cancellationToken)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            PublishedNotification = notification;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificationsRepository : IAdminNotificationsRepository
    {
        public QueuedAdminTestNotification Queued { get; } = new(
            Guid.Parse("99999999-9999-9999-9999-999999999999"),
            new RealtimeNotificationPayload(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                TenantId,
                UserId,
                "Talent Pilot notification test",
                "Your realtime in-app notifications are connected.",
                "AdminNotificationTest",
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                null,
                DateTimeOffset.Parse("2026-05-22T10:00:00Z"),
                "ADMIN_TEST_NOTIFICATION"));

        public bool QueueCalled { get; private set; }

        public Guid LastTenantId { get; private set; }

        public Guid LastActorUserId { get; private set; }

        public string LastActorEmail { get; private set; } = string.Empty;

        public List<(Guid OutboxId, string Status, string? LastError)> StatusUpdates { get; } = [];

        public Task<QueuedAdminTestNotification> QueueTestNotificationAsync(
            Guid tenantId,
            Guid actorUserId,
            string actorEmail,
            string title,
            string message,
            CancellationToken cancellationToken)
        {
            QueueCalled = true;
            LastTenantId = tenantId;
            LastActorUserId = actorUserId;
            LastActorEmail = actorEmail;
            Assert.False(string.IsNullOrWhiteSpace(title));
            Assert.False(string.IsNullOrWhiteSpace(message));
            return Task.FromResult(Queued);
        }

        public Task UpdateOutboxStatusAsync(Guid tenantId, Guid outboxId, string status, string? lastError, CancellationToken cancellationToken)
        {
            StatusUpdates.Add((outboxId, status, lastError));
            return Task.CompletedTask;
        }

        public Task<AdminNotificationEventsResponse> ListEventsAsync(Guid tenantId, AdminNotificationEventsQuery query, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<AdminNotificationEventDetails?> GetEventAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<NotificationTemplateSummary>> ListTemplatesAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<NotificationTemplateSummary?> GetTemplateAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task UpdateTemplateAsync(Guid tenantId, Guid actorUserId, Guid templateId, UpdateNotificationTemplateInput input, string metadataJson, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task UpdateEventStatusAsync(Guid tenantId, Guid actorUserId, Guid eventId, string status, string metadataJson, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
