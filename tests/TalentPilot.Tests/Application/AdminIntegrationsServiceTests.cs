using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Admin.Integrations;
using TalentPilot.Application.Admin.Notifications;

namespace TalentPilot.Tests.Application;

public sealed class AdminIntegrationsServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetStatusAsync_ReturnsReadOnlyMvpIntegrationContracts()
    {
        var service = new AdminIntegrationsService(
            new FakeNotificationsRepository(new AdminNotificationEventsSummary(ActiveEventCount: 3, EditableTemplateCount: 2, PendingOutboxCount: 1, FailedOutboxCount: 0)),
            new FakeRuntimeSettings(),
            new FakeCurrentUser());

        var result = await service.GetStatusAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Value.ReadOnly);
        Assert.Equal(6, result.Value.TotalCount);
        Assert.All(result.Value.Items, item => Assert.False(item.Editable));
        Assert.Contains(result.Value.Items, item => item.Id == "email-outbox" && item.Status == "Available");
        Assert.Contains(result.Value.Items, item => item.Id == "signalr-in-app" && item.Enabled);
        Assert.Contains(result.Value.Items, item => item.Id == "linkedin-mock-publishing" && item.Status == "Mock Only" && !item.Enabled);
        Assert.Contains(result.Value.Items, item => item.Id == "ai-runtime" && item.RuntimeMode.Contains("Mock/Ollama", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetStatusAsync_WhenOutboxHasFailures_MarksEmailOutboxDegraded()
    {
        var service = new AdminIntegrationsService(
            new FakeNotificationsRepository(new AdminNotificationEventsSummary(ActiveEventCount: 3, EditableTemplateCount: 2, PendingOutboxCount: 4, FailedOutboxCount: 2)),
            new FakeRuntimeSettings(),
            new FakeCurrentUser());

        var result = await service.GetStatusAsync(CancellationToken.None);

        var emailOutbox = Assert.Single(result.Value.Items, item => item.Id == "email-outbox");
        Assert.Equal("Degraded", emailOutbox.Status);
        Assert.Contains(emailOutbox.Metrics, metric => metric.Name == "FailedOutbox" && metric.Value == 2);
    }

    private sealed class FakeCurrentUser : ICurrentUserAccessor
    {
        public Guid UserId => AdminIntegrationsServiceTests.UserId;

        public Guid TenantId => AdminIntegrationsServiceTests.TenantId;

        public string Email => "tenant.admin@tkxel.com";

        public IReadOnlySet<string> RoleCodes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FakeRuntimeSettings : IAdminRuntimeSettings
    {
        public string Provider => "Mock/Ollama";

        public string LlmModel => "llama3.1:8b";

        public string EmbeddingModel => "nomic-embed-text";

        public int EmbeddingDimensions => 768;
    }

    private sealed class FakeNotificationsRepository : IAdminNotificationsRepository
    {
        private readonly AdminNotificationEventsSummary _summary;

        public FakeNotificationsRepository(AdminNotificationEventsSummary summary)
        {
            _summary = summary;
        }

        public Task<AdminNotificationEventsResponse> ListEventsAsync(
            Guid tenantId,
            AdminNotificationEventsQuery query,
            CancellationToken cancellationToken)
        {
            var response = new AdminNotificationEventsResponse(_summary, [], query.Page, query.PageSize, TotalCount: 0);
            return Task.FromResult(response);
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

        public Task UpdateTemplateAsync(
            Guid tenantId,
            Guid actorUserId,
            Guid templateId,
            UpdateNotificationTemplateInput input,
            string metadataJson,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task UpdateEventStatusAsync(
            Guid tenantId,
            Guid actorUserId,
            Guid eventId,
            string status,
            string metadataJson,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<QueuedAdminTestNotification> QueueTestNotificationAsync(
            Guid tenantId,
            Guid actorUserId,
            string actorEmail,
            string title,
            string message,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task UpdateOutboxStatusAsync(
            Guid tenantId,
            Guid outboxId,
            string status,
            string? lastError,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
