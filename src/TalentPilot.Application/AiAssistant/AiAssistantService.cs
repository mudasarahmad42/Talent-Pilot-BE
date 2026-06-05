using System.Security.Cryptography;
using System.Text;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Operations;
using TalentPilot.Common.Results;
using TalentPilot.Domain.Access;

namespace TalentPilot.Application.AiAssistant;

public sealed class AiAssistantService : IAiAssistantService
{
    private const string AgentId = "conversational-rag-assistant";
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IOperationsRepository _operationsRepository;
    private readonly IKnowledgeRepository _knowledgeRepository;
    private readonly IKnowledgeIndexingService _indexingService;
    private readonly IKnowledgeRetrievalService _retrievalService;
    private readonly IRagPromptBuilder _promptBuilder;
    private readonly IAiModelProvider _modelProvider;
    private readonly IAiRuntimeSettingsResolver _settingsResolver;
    private readonly IAiAgentRunLogger _runLogger;

    public AiAssistantService(
        ICurrentUserAccessor currentUser,
        IOperationsRepository operationsRepository,
        IKnowledgeRepository knowledgeRepository,
        IKnowledgeIndexingService indexingService,
        IKnowledgeRetrievalService retrievalService,
        IRagPromptBuilder promptBuilder,
        IAiModelProvider modelProvider,
        IAiRuntimeSettingsResolver settingsResolver,
        IAiAgentRunLogger runLogger)
    {
        _currentUser = currentUser;
        _operationsRepository = operationsRepository;
        _knowledgeRepository = knowledgeRepository;
        _indexingService = indexingService;
        _retrievalService = retrievalService;
        _promptBuilder = promptBuilder;
        _modelProvider = modelProvider;
        _settingsResolver = settingsResolver;
        _runLogger = runLogger;
    }

    public async Task<Result<RagChatResponse>> SendMessageAsync(
        RagChatRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequest(request);
        if (normalized is null)
        {
            return Result<RagChatResponse>.Failure("ai_assistant.invalid_request", "Message and a supported assistant context are required.");
        }

        var authorization = await AuthorizeAsync(normalized, cancellationToken);
        if (authorization.Failed)
        {
            return Result<RagChatResponse>.Failure(authorization.Error.Code, authorization.Error.Message);
        }

        var conversation = normalized.ConversationId.HasValue
            ? await _knowledgeRepository.GetConversationAsync(
                _currentUser.TenantId,
                _currentUser.UserId,
                normalized.ConversationId.Value,
                cancellationToken)
            : null;

        if (normalized.ConversationId.HasValue && conversation is null)
        {
            return Result<RagChatResponse>.Failure("ai_assistant.conversation_not_found", "Assistant conversation was not found.");
        }

        var conversationId = conversation?.ConversationId
            ?? await _knowledgeRepository.CreateConversationAsync(
                _currentUser.TenantId,
                _currentUser.UserId,
                normalized.ContextType,
                normalized.ContextEntityId,
                normalized.FocusEntityId,
                BuildTitle(normalized.Message),
                cancellationToken);

        var userMessageId = await _knowledgeRepository.AddMessageAsync(
            _currentUser.TenantId,
            conversationId,
            "User",
            normalized.Message,
            null,
            null,
            null,
            null,
            null,
            Array.Empty<Guid>(),
            cancellationToken);

        try
        {
            if (RagSensitiveRequestGuard.IsCredentialDisclosureRequest(normalized.Message))
            {
                var safetyResponse = await CreateCredentialSafetyResponseAsync(
                    normalized,
                    conversationId,
                    userMessageId,
                    cancellationToken);
                return Result<RagChatResponse>.Success(safetyResponse);
            }

            await _indexingService.EnsureContextIndexedAsync(
                _currentUser.TenantId,
                _currentUser.UserId,
                normalized.ContextType,
                normalized.ContextEntityId,
                normalized.FocusEntityId,
                cancellationToken);

            var evidence = await _retrievalService.RetrieveAsync(
                _currentUser.TenantId,
                _currentUser.UserId,
                normalized.ContextType,
                normalized.ContextEntityId,
                normalized.FocusEntityId,
                normalized.Message,
                cancellationToken);

            if (evidence.Count == 0)
            {
                var noEvidenceMessageId = await _knowledgeRepository.AddMessageAsync(
                    _currentUser.TenantId,
                    conversationId,
                    "Assistant",
                    "I do not have enough indexed evidence to answer that question.",
                    null,
                    null,
                    RagPromptBuilder.CurrentPromptVersion,
                    "ai_assistant.no_evidence",
                    "No relevant evidence chunks were found for this context.",
                    Array.Empty<Guid>(),
                    cancellationToken);

                return Result<RagChatResponse>.Failure(
                    "ai_assistant.no_evidence",
                    $"No relevant evidence was found. Conversation: {conversationId}, assistant message: {noEvidenceMessageId}");
            }

            var latestConversation = await _knowledgeRepository.GetConversationAsync(
                _currentUser.TenantId,
                _currentUser.UserId,
                conversationId,
                cancellationToken);
            var prompt = _promptBuilder.Build(new RagPromptContext(
                normalized.ContextType,
                normalized.Message,
                latestConversation?.Messages ?? Array.Empty<RagMessage>(),
                evidence));

            var settings = await _settingsResolver.GetCurrentAsync(cancellationToken);
            var runId = await _runLogger.StartAsync(
                new AiAgentRunStart(
                    _currentUser.TenantId,
                    AgentId,
                    normalized.ContextType,
                    normalized.ContextEntityId,
                    settings.LlmModel,
                    settings.EmbeddingModel,
                    Sha256(prompt.Prompt),
                    new Dictionary<string, string>
                    {
                        ["promptVersion"] = prompt.PromptVersion,
                        ["conversationId"] = conversationId.ToString(),
                        ["focusEntityId"] = normalized.FocusEntityId?.ToString() ?? string.Empty,
                        ["retrievedChunkIds"] = string.Join(",", evidence.Select(chunk => chunk.KnowledgeChunkId))
                    }),
                cancellationToken);

            string answer;
            IReadOnlyList<RagCitationDraft> usedCitations;
            try
            {
                answer = await _modelProvider.GenerateAsync(
                    new AiPromptRequest(
                        AgentId,
                        prompt.Prompt,
                        new Dictionary<string, string>
                        {
                            ["contextType"] = normalized.ContextType,
                            ["promptVersion"] = prompt.PromptVersion
                        }),
                    cancellationToken);
                usedCitations = RagCitationUsage.FilterReferenced(prompt.Citations, answer);

                await _runLogger.SucceedAsync(
                    _currentUser.TenantId,
                    runId,
                    Trim(answer, 900),
                    new Dictionary<string, string>
                    {
                        ["promptVersion"] = prompt.PromptVersion,
                        ["citationCount"] = usedCitations.Count.ToString(),
                        ["retrievedCitationCount"] = prompt.Citations.Count.ToString()
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                await _runLogger.FailAsync(
                    _currentUser.TenantId,
                    runId,
                    ex.Message,
                    new Dictionary<string, string> { ["promptVersion"] = prompt.PromptVersion },
                    cancellationToken);
                var failedMessageId = await _knowledgeRepository.AddMessageAsync(
                    _currentUser.TenantId,
                    conversationId,
                    "Assistant",
                    "AI runtime unavailable. The assistant could not generate an evidence-based answer right now.",
                    settings.LlmModel,
                    runId,
                    prompt.PromptVersion,
                    "ai_assistant.runtime_unavailable",
                    "The configured AI runtime is unavailable.",
                    evidence.Select(chunk => chunk.KnowledgeChunkId).ToArray(),
                    cancellationToken);

                return Result<RagChatResponse>.Failure(
                    "ai_assistant.runtime_unavailable",
                    $"AI runtime unavailable. Conversation: {conversationId}, assistant message: {failedMessageId}");
            }

            var assistantMessageId = await _knowledgeRepository.AddMessageAsync(
                _currentUser.TenantId,
                conversationId,
                "Assistant",
                answer,
                settings.LlmModel,
                runId,
                prompt.PromptVersion,
                null,
                null,
                evidence.Select(chunk => chunk.KnowledgeChunkId).ToArray(),
                cancellationToken);

            await _knowledgeRepository.SaveCitationsAsync(
                _currentUser.TenantId,
                assistantMessageId,
                usedCitations,
                cancellationToken);

            var persisted = await _knowledgeRepository.GetConversationAsync(
                _currentUser.TenantId,
                _currentUser.UserId,
                conversationId,
                cancellationToken);
            var citations = persisted?.Messages.FirstOrDefault(message => message.MessageId == assistantMessageId)?.Citations
                ?? Array.Empty<RagCitation>();

            return Result<RagChatResponse>.Success(new RagChatResponse(
                conversationId,
                userMessageId,
                assistantMessageId,
                answer,
                citations,
                settings.LlmModel,
                runId,
                prompt.PromptVersion,
                DateTimeOffset.UtcNow));
        }
        catch (Exception)
        {
            return Result<RagChatResponse>.Failure(
                "ai_assistant.runtime_unavailable",
                "AI assistant could not complete the request because the configured AI runtime is unavailable.");
        }
    }

    private async Task<RagChatResponse> CreateCredentialSafetyResponseAsync(
        RagChatRequest request,
        Guid conversationId,
        Guid userMessageId,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.GetCurrentAsync(cancellationToken);
        var answer = RagSensitiveRequestGuard.BuildCredentialRefusal(request.ContextType);
        var runId = await _runLogger.StartAsync(
            new AiAgentRunStart(
                _currentUser.TenantId,
                AgentId,
                request.ContextType,
                request.ContextEntityId,
                settings.LlmModel,
                settings.EmbeddingModel,
                Sha256($"{RagPromptBuilder.CurrentPromptVersion}:credential-safety:{request.Message}"),
                new Dictionary<string, string>
                {
                    ["promptVersion"] = RagPromptBuilder.CurrentPromptVersion,
                    ["conversationId"] = conversationId.ToString(),
                    ["focusEntityId"] = request.FocusEntityId?.ToString() ?? string.Empty,
                    ["safetyGuard"] = "credential-disclosure",
                    ["retrievedChunkIds"] = string.Empty
                }),
            cancellationToken);

        await _runLogger.SucceedAsync(
            _currentUser.TenantId,
            runId,
            Trim(answer, 900),
            new Dictionary<string, string>
            {
                ["promptVersion"] = RagPromptBuilder.CurrentPromptVersion,
                ["citationCount"] = "0",
                ["retrievedCitationCount"] = "0",
                ["safetyGuard"] = "credential-disclosure"
            },
            cancellationToken);

        var assistantMessageId = await _knowledgeRepository.AddMessageAsync(
            _currentUser.TenantId,
            conversationId,
            "Assistant",
            answer,
            settings.LlmModel,
            runId,
            RagPromptBuilder.CurrentPromptVersion,
            null,
            null,
            Array.Empty<Guid>(),
            cancellationToken);

        return new RagChatResponse(
            conversationId,
            userMessageId,
            assistantMessageId,
            answer,
            Array.Empty<RagCitation>(),
            settings.LlmModel,
            runId,
            RagPromptBuilder.CurrentPromptVersion,
            DateTimeOffset.UtcNow);
    }

    public async Task<Result<IReadOnlyList<RagConversation>>> ListConversationsAsync(
        string? contextType,
        Guid? contextEntityId,
        Guid? focusEntityId,
        CancellationToken cancellationToken)
    {
        if (!await HasAssistantPermissionAsync(cancellationToken))
        {
            return Result<IReadOnlyList<RagConversation>>.Failure("ai_assistant.forbidden", "You do not have access to the AI assistant.");
        }

        var normalizedContext = string.IsNullOrWhiteSpace(contextType) ? null : RagAssistantContextTypes.Normalize(contextType);
        var conversations = await _knowledgeRepository.ListConversationsAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            normalizedContext,
            contextEntityId,
            focusEntityId,
            cancellationToken);
        return Result<IReadOnlyList<RagConversation>>.Success(conversations);
    }

    public async Task<Result<RagConversation>> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (!await HasAssistantPermissionAsync(cancellationToken))
        {
            return Result<RagConversation>.Failure("ai_assistant.forbidden", "You do not have access to the AI assistant.");
        }

        var conversation = await _knowledgeRepository.GetConversationAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            conversationId,
            cancellationToken);
        return conversation is null
            ? Result<RagConversation>.Failure("ai_assistant.conversation_not_found", "Assistant conversation was not found.")
            : Result<RagConversation>.Success(conversation);
    }

    public async Task<Result> SubmitFeedbackAsync(
        Guid messageId,
        RagFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        if (!await HasAssistantPermissionAsync(cancellationToken))
        {
            return Result.Failure("ai_assistant.forbidden", "You do not have access to the AI assistant.");
        }

        var rating = request.Rating.Trim();
        if (!string.Equals(rating, "Helpful", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(rating, "NotHelpful", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("ai_assistant.feedback_invalid", "Feedback rating must be Helpful or NotHelpful.");
        }

        await _knowledgeRepository.SaveFeedbackAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            messageId,
            string.Equals(rating, "Helpful", StringComparison.OrdinalIgnoreCase) ? "Helpful" : "NotHelpful",
            request.Notes,
            cancellationToken);
        return Result.Success();
    }

    public async Task<Result<RagRebuildIndexResponse>> RebuildIndexAsync(
        RagRebuildIndexRequest request,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _operationsRepository.GetActorRoleCodesAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            cancellationToken);
        if (!roleCodes.Contains(AccessConstants.TenantAdminRoleCode) && !roleCodes.Contains(AccessConstants.SystemAdminRoleCode))
        {
            return Result<RagRebuildIndexResponse>.Failure(
                "ai_assistant.rebuild_forbidden",
                "Only Tenant Admin or System Admin users can rebuild the assistant knowledge index.");
        }

        if (string.IsNullOrWhiteSpace(request.ContextType) || !request.ContextEntityId.HasValue)
        {
            return Result<RagRebuildIndexResponse>.Failure(
                "ai_assistant.rebuild_context_required",
                "MVP rebuild requires a context type and context entity id.");
        }

        var contextType = RagAssistantContextTypes.Normalize(request.ContextType);
        var authorization = await AuthorizeAsync(
            new RagChatRequest(contextType, request.ContextEntityId.Value, request.FocusEntityId, null, "Rebuild index"),
            cancellationToken);
        if (authorization.Failed)
        {
            return Result<RagRebuildIndexResponse>.Failure(authorization.Error.Code, authorization.Error.Message);
        }

        var chunks = await _indexingService.EnsureContextIndexedAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            contextType,
            request.ContextEntityId.Value,
            request.FocusEntityId,
            cancellationToken);

        return Result<RagRebuildIndexResponse>.Success(new RagRebuildIndexResponse(
            1,
            chunks.Count,
            DateTimeOffset.UtcNow));
    }

    private async Task<Result> AuthorizeAsync(RagChatRequest request, CancellationToken cancellationToken)
    {
        if (!await HasAssistantPermissionAsync(cancellationToken))
        {
            return Result.Failure("ai_assistant.forbidden", "You do not have access to the AI assistant.");
        }

        var roleCodes = await _operationsRepository.GetActorRoleCodesAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            cancellationToken);

        if (request.ContextType == RagAssistantContextTypes.PmoRequest)
        {
            if (!roleCodes.Contains("PMO") && !roleCodes.Contains("TenantAdmin"))
            {
                return Result.Failure("ai_assistant.pmo_forbidden", "Only PMO or Tenant Admin users can use the request copilot.");
            }

            var review = await _operationsRepository.GetPmoReviewAsync(
                _currentUser.TenantId,
                _currentUser.UserId,
                request.ContextEntityId,
                includeEmployees: true,
                cancellationToken);
            return review is null
                ? Result.Failure("ai_assistant.context_not_found", "Assistant context was not found or is not visible.")
                : Result.Success();
        }

        if (request.ContextType == RagAssistantContextTypes.RecruiterCandidateFit)
        {
            if (!roleCodes.Contains("Recruiter") && !roleCodes.Contains("TenantAdmin"))
            {
                return Result.Failure("ai_assistant.recruiter_forbidden", "Only Recruiter or Tenant Admin users can use the candidate fit assistant.");
            }

            var sourcing = await _operationsRepository.GetRecruiterSourcingAsync(
                _currentUser.TenantId,
                _currentUser.UserId,
                request.ContextEntityId,
                cancellationToken);
            var visible = sourcing is not null
                && (!request.FocusEntityId.HasValue || sourcing.Applications.Any(application => application.JobApplicationId == request.FocusEntityId.Value));
            return visible
                ? Result.Success()
                : Result.Failure("ai_assistant.context_not_found", "Assistant context was not found or is not visible.");
        }

        if (request.ContextType == RagAssistantContextTypes.HiringDecisionBrief)
        {
            if (!roleCodes.Contains("HiringManager") && !roleCodes.Contains("TenantAdmin"))
            {
                return Result.Failure("ai_assistant.hiring_forbidden", "Only Hiring Manager or Tenant Admin users can use the decision assistant.");
            }

            var detail = await _operationsRepository.GetHiringReviewAsync(
                _currentUser.TenantId,
                _currentUser.UserId,
                roleCodes.Contains("TenantAdmin"),
                request.ContextEntityId,
                cancellationToken);
            return detail is null
                ? Result.Failure("ai_assistant.context_not_found", "Assistant context was not found or is not visible.")
                : Result.Success();
        }

        return Result.Failure("ai_assistant.invalid_context", "Unsupported assistant context type.");
    }

    private async Task<bool> HasAssistantPermissionAsync(CancellationToken cancellationToken)
    {
        var permissions = await _knowledgeRepository.GetActorPermissionIdsAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            cancellationToken);
        return permissions.Contains(AccessConstants.AiAssistantUse);
    }

    private static RagChatRequest? NormalizeRequest(RagChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message)
            || string.IsNullOrWhiteSpace(request.ContextType)
            || !RagAssistantContextTypes.IsKnown(request.ContextType))
        {
            return null;
        }

        return request with
        {
            ContextType = RagAssistantContextTypes.Normalize(request.ContextType),
            Message = request.Message.Trim()
        };
    }

    private static string BuildTitle(string message)
    {
        return Trim(message.Trim(), 80);
    }

    private static string Trim(string value, int length)
    {
        return value.Length <= length ? value : $"{value[..length]}...";
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
