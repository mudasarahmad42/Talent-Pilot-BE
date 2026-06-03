using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Application.Notifications;
using TalentPilot.Common.Results;

namespace TalentPilot.Tests.Admin;

public sealed class AdminNotificationsServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task RetryOutboxEmailAsync_RequeuesFailedEmail()
    {
        var outboxId = Guid.NewGuid();
        var repository = new StubAdminNotificationsRepository(OutboxItem(outboxId, "Failed", "Provider rejected the email."));
        var service = CreateService(repository);

        var result = await service.RetryOutboxEmailAsync(outboxId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(outboxId, repository.RequeuedOutboxId);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal(1, result.Value.AttemptCount);
        Assert.Null(result.Value.LastError);
        Assert.Null(result.Value.ProcessedAtUtc);
    }

    [Fact]
    public async Task RetryOutboxEmailAsync_RejectsNonFailedEmail()
    {
        var outboxId = Guid.NewGuid();
        var repository = new StubAdminNotificationsRepository(OutboxItem(outboxId, "Sent", null));
        var service = CreateService(repository);

        var result = await service.RetryOutboxEmailAsync(outboxId, CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("notifications.outbox_retry_invalid", result.Error.Code);
        Assert.Null(repository.RequeuedOutboxId);
    }

    private static AdminNotificationsService CreateService(StubAdminNotificationsRepository repository)
    {
        return new AdminNotificationsService(
            repository,
            new StubNotificationEmailSender(),
            new StubRealtimeNotificationPublisher(),
            new StubRealtimeConnectionCounter(),
            new StubCurrentUserAccessor());
    }

    private static AdminNotificationOutboxItem OutboxItem(Guid outboxId, string status, string? lastError)
    {
        var now = DateTimeOffset.UtcNow;
        return new AdminNotificationOutboxItem(
            outboxId,
            "CANDIDATE_INVITED_TO_APPLY",
            "Candidate invited to apply",
            "Application-composed email",
            "Talent Pilot workflow",
            "Alex Morgan",
            "alex@example.com",
            "Email",
            status,
            1,
            now,
            now.AddMinutes(-10),
            now,
            status == "Failed" ? now : null,
            lastError,
            "TKXEL Careers is looking for Senior React Developer",
            "Please apply on our job portal.",
            "JobPost",
            Guid.NewGuid().ToString("D"));
    }

    private sealed class StubAdminNotificationsRepository : IAdminNotificationsRepository
    {
        private AdminNotificationOutboxItem? _item;

        public StubAdminNotificationsRepository(AdminNotificationOutboxItem? item)
        {
            _item = item;
        }

        public Guid? RequeuedOutboxId { get; private set; }

        public Task<AdminNotificationOutboxItem?> GetOutboxItemAsync(Guid tenantId, Guid outboxId, CancellationToken cancellationToken)
        {
            return Task.FromResult(tenantId == TenantId && _item?.OutboxId == outboxId ? _item : null);
        }

        public Task<AdminNotificationOutboxItem?> RequeueOutboxEmailAsync(Guid tenantId, Guid outboxId, CancellationToken cancellationToken)
        {
            if (tenantId != TenantId ||
                _item?.OutboxId != outboxId ||
                !string.Equals(_item.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<AdminNotificationOutboxItem?>(null);
            }

            RequeuedOutboxId = outboxId;
            _item = _item with
            {
                Status = "Pending",
                AvailableAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                ProcessedAtUtc = null,
                LastError = null
            };
            return Task.FromResult<AdminNotificationOutboxItem?>(_item);
        }

        public Task<AdminNotificationEventsResponse> ListEventsAsync(Guid tenantId, AdminNotificationEventsQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AdminNotificationEventDetails?> GetEventAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AdminNotificationTemplatesResponse> ListTemplatesAsync(Guid tenantId, AdminNotificationTemplatesQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AdminNotificationOutboxResponse> ListOutboxAsync(Guid tenantId, AdminNotificationOutboxQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<NotificationTemplateSummary?> GetTemplateAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task UpdateTemplateAsync(Guid tenantId, Guid actorUserId, Guid templateId, UpdateNotificationTemplateInput input, string metadataJson, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RecordTestEmailSentAsync(Guid tenantId, Guid actorUserId, string recipientEmail, string providerMessageId, string metadataJson, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RecordRealtimeTestNotificationSentAsync(Guid tenantId, Guid actorUserId, Guid notificationId, int connectedClientCount, string metadataJson, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task UpdateEventStatusAsync(Guid tenantId, Guid actorUserId, Guid eventId, string status, string metadataJson, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubNotificationEmailSender : INotificationEmailSender
    {
        public Task<Result<NotificationEmailSendResult>> SendAsync(NotificationEmailMessage message, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubRealtimeNotificationPublisher : IRealtimeNotificationPublisher
    {
        public Task<RealtimeNotificationPublishResult> PublishToTenantAsync(
            Guid tenantId,
            RealtimeNotificationMessage notification,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RealtimeNotificationPublishResult> PublishToUserAsync(
            Guid tenantId,
            Guid userId,
            RealtimeNotificationMessage notification,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RealtimeNotificationPublishResult> PublishToAllAsync(
            RealtimeNotificationMessage notification,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubRealtimeConnectionCounter : IRealtimeConnectionCounter
    {
        public int CountTenantConnections(Guid tenantId) => 0;
    }

    private sealed class StubCurrentUserAccessor : ICurrentUserAccessor
    {
        public Guid UserId => AdminNotificationsServiceTests.UserId;

        public Guid TenantId => AdminNotificationsServiceTests.TenantId;

        public string Email => "admin@example.com";
    }
}
