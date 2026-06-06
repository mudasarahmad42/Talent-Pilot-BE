using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TalentPilot.Api.Security;
using TalentPilot.Application.Feedback;

namespace TalentPilot.Api.Controllers;

[AllowAnonymous]
[Route("api/feedback")]
[EnableRateLimiting(ApiRateLimitPolicies.PublicPortal)]
public sealed class FeedbackController : ApiControllerBase
{
    private const string DefaultRecipientEmail = "mudasarahmad150@gmail.com";
    private const string DefaultSenderEmail = "mudasar.ahmad@8pkk57.onmicrosoft.com";

    private readonly IPublicFeedbackService _feedbackService;
    private readonly IConfiguration _configuration;

    public FeedbackController(
        IPublicFeedbackService feedbackService,
        IConfiguration configuration)
    {
        _feedbackService = feedbackService;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<ActionResult<SubmitPublicFeedbackResponse>> Submit(
        PublicFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var recipientEmail = _configuration["PublicFeedback:RecipientEmail"] ?? DefaultRecipientEmail;
        var senderEmail = _configuration["PublicFeedback:SenderEmail"] ?? DefaultSenderEmail;
        var input = new SubmitPublicFeedbackInput(
            request.Name,
            request.Email,
            request.Message,
            request.TenantSlug,
            request.JobPostId,
            recipientEmail,
            senderEmail);

        return FromResult(await _feedbackService.SubmitAsync(input, cancellationToken));
    }
}

public sealed record PublicFeedbackRequest(
    string? Name,
    string? Email,
    string? Message,
    string? TenantSlug,
    Guid? JobPostId);
