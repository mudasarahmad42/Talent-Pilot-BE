using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TalentPilot.Api.Security;
using TalentPilot.Application.AiAssistant;

namespace TalentPilot.Api.Controllers;

[Route("api/talent-pilot/ai-assistant")]
public sealed class AiAssistantController : ApiControllerBase
{
    private readonly IAiAssistantService _assistantService;

    public AiAssistantController(IAiAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    [HttpPost("messages")]
    [EnableRateLimiting(ApiRateLimitPolicies.AiWork)]
    public async Task<ActionResult<RagChatResponse>> SendMessage(
        [FromBody] RagChatRequest request,
        CancellationToken cancellationToken)
    {
        return FromResult(await _assistantService.SendMessageAsync(request, cancellationToken));
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<RagConversation>>> ListConversations(
        [FromQuery] string? contextType,
        [FromQuery] Guid? contextEntityId,
        [FromQuery] Guid? focusEntityId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _assistantService.ListConversationsAsync(
            contextType,
            contextEntityId,
            focusEntityId,
            cancellationToken));
    }

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<RagConversation>> GetConversation(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _assistantService.GetConversationAsync(conversationId, cancellationToken));
    }

    [HttpPost("messages/{messageId:guid}/feedback")]
    public async Task<IActionResult> SubmitFeedback(
        Guid messageId,
        [FromBody] RagFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        return FromResult(await _assistantService.SubmitFeedbackAsync(messageId, request, cancellationToken));
    }

    [HttpPost("index/rebuild")]
    [EnableRateLimiting(ApiRateLimitPolicies.AiWork)]
    public async Task<ActionResult<RagRebuildIndexResponse>> RebuildIndex(
        [FromBody] RagRebuildIndexRequest request,
        CancellationToken cancellationToken)
    {
        return FromResult(await _assistantService.RebuildIndexAsync(request, cancellationToken));
    }
}
