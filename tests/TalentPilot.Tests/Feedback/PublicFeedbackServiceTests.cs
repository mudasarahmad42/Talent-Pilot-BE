using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Application.Feedback;
using TalentPilot.Application.Notifications;
using TalentPilot.Common.Results;

namespace TalentPilot.Tests.Feedback;

public sealed class PublicFeedbackServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AuthenticatedTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task SubmitAsync_SendsAdminAndThankYouEmails()
    {
        var emailSender = new CapturingEmailSender();
        var service = CreateService(emailSender: emailSender);

        var result = await service.SubmitAsync(ValidInput(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, emailSender.Messages.Count);
        Assert.Equal("mudasarahmad150@gmail.com", emailSender.Messages[0].ToEmail);
        Assert.Equal("visitor@example.com", emailSender.Messages[1].ToEmail);
        Assert.Equal("mudasar.ahmad@8pkk57.onmicrosoft.com", emailSender.Messages[0].FromEmail);
        Assert.Equal("mudasar.ahmad@8pkk57.onmicrosoft.com", emailSender.Messages[1].FromEmail);
        Assert.Contains("New feedback received", emailSender.Messages[0].HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("We received your feedback", emailSender.Messages[1].HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("more dashboard guidance", emailSender.Messages[1].HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(TalentPilotEmailTemplate.TemplateMarker, emailSender.Messages[0].HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(TalentPilotEmailTemplate.TemplateMarker, emailSender.Messages[1].HtmlBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitAsync_RejectsInvalidEmailWithoutSending()
    {
        var emailSender = new CapturingEmailSender();
        var service = CreateService(emailSender: emailSender);

        var result = await service.SubmitAsync(ValidInput() with { Email = "not-an-email" }, CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("feedback.email_invalid", result.Error.Code);
        Assert.Empty(emailSender.Messages);
    }

    [Fact]
    public async Task SubmitAsync_UsesAuthenticatedTenantWhenPublicTenantCannotBeResolved()
    {
        var emailSender = new CapturingEmailSender();
        var service = CreateService(
            tenantResolver: new StubTenantResolver(null),
            emailSender: emailSender,
            currentUser: new StubCurrentUserAccessor(AuthenticatedTenantId));

        var result = await service.SubmitAsync(ValidInput() with { TenantSlug = null, JobPostId = null }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.All(emailSender.Messages, message => Assert.Equal(AuthenticatedTenantId, message.TenantId));
    }

    [Fact]
    public async Task SubmitAsync_SucceedsWhenThankYouEmailFailsAfterAdminEmail()
    {
        var emailSender = new CapturingEmailSender(failOnAttempt: 2);
        var service = CreateService(emailSender: emailSender);

        var result = await service.SubmitAsync(ValidInput(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, emailSender.Messages.Count);
        Assert.Equal("mudasarahmad150@gmail.com", emailSender.Messages[0].ToEmail);
        Assert.Equal("visitor@example.com", emailSender.Messages[1].ToEmail);
    }

    [Fact]
    public async Task SubmitAsync_ReturnsGenericErrorWhenAdminEmailFails()
    {
        var emailSender = new CapturingEmailSender(failOnAttempt: 1);
        var service = CreateService(emailSender: emailSender);

        var result = await service.SubmitAsync(ValidInput(), CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("feedback.delivery_failed", result.Error.Code);
        Assert.Equal("Feedback could not be sent right now. Please try again shortly.", result.Error.Message);
    }

    [Fact]
    public async Task SubmitAsync_RejectsAnonymousFeedbackWhenTenantIsAmbiguous()
    {
        var service = CreateService(
            tenantResolver: new StubTenantResolver(null),
            currentUser: new StubCurrentUserAccessor(Guid.Empty));

        var result = await service.SubmitAsync(ValidInput() with { TenantSlug = null, JobPostId = null }, CancellationToken.None);

        Assert.True(result.Failed);
        Assert.Equal("feedback.tenant_not_found", result.Error.Code);
    }

    private static SubmitPublicFeedbackInput ValidInput() => new(
        "Site Visitor",
        "visitor@example.com",
        "The product flow is helpful and I would like to see more dashboard guidance.",
        "tkxel",
        null,
        "mudasarahmad150@gmail.com",
        "mudasar.ahmad@8pkk57.onmicrosoft.com");

    private static PublicFeedbackService CreateService(
        IPublicFeedbackTenantResolver? tenantResolver = null,
        CapturingEmailSender? emailSender = null,
        ICurrentUserAccessor? currentUser = null)
    {
        return new PublicFeedbackService(
            tenantResolver ?? new StubTenantResolver(new PublicFeedbackTenant(TenantId, "TKXEL", "tkxel")),
            emailSender ?? new CapturingEmailSender(),
            currentUser ?? new StubCurrentUserAccessor(Guid.Empty));
    }

    private sealed class StubTenantResolver : IPublicFeedbackTenantResolver
    {
        private readonly PublicFeedbackTenant? _tenant;

        public StubTenantResolver(PublicFeedbackTenant? tenant)
        {
            _tenant = tenant;
        }

        public Task<PublicFeedbackTenant?> ResolveAsync(PublicFeedbackTenantQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(_tenant);
    }

    private sealed class CapturingEmailSender : INotificationEmailSender
    {
        private readonly int? _failOnAttempt;

        public CapturingEmailSender(int? failOnAttempt = null)
        {
            _failOnAttempt = failOnAttempt;
        }

        public List<NotificationEmailMessage> Messages { get; } = [];

        public Task<Result<NotificationEmailSendResult>> SendAsync(NotificationEmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            if (_failOnAttempt == Messages.Count)
            {
                return Task.FromResult(Result<NotificationEmailSendResult>.Failure(
                    "test.email_failed",
                    "Provider test failure."));
            }

            return Task.FromResult(Result<NotificationEmailSendResult>.Success(new NotificationEmailSendResult(
                "Test",
                $"message-{Messages.Count}",
                DateTimeOffset.UtcNow)));
        }
    }

    private sealed class StubCurrentUserAccessor : ICurrentUserAccessor
    {
        public StubCurrentUserAccessor(Guid tenantId)
        {
            TenantId = tenantId;
        }

        public Guid UserId => Guid.NewGuid();

        public Guid TenantId { get; }

        public string Email => "tester@example.com";

        public IReadOnlyCollection<string> RoleCodes => Array.Empty<string>();
    }
}
