using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.AiSettings;

public interface IAdminAiSettingsService
{
    Task<Result<AdminAiRuntimeResponse>> GetRuntimeAsync(CancellationToken cancellationToken);

    Task<Result<AdminLlmHealthResponse>> GetLlmHealthAsync(CancellationToken cancellationToken);

    Task<Result<AdminSemanticSimilarityHealthResponse>> GetSemanticSimilarityHealthAsync(CancellationToken cancellationToken);

    Task<Result<AdminAiAgentListResponse>> GetAgentsAsync(CancellationToken cancellationToken);

    Task<Result<AdminAiGuardrailsResponse>> GetGuardrailsAsync(CancellationToken cancellationToken);
}

public interface IAdminAiSettingsRepository
{
    Task<AdminAiRuntimeResponse?> GetRuntimeAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminAiAgentDefinition>> ListAgentsAsync(CancellationToken cancellationToken);

    Task<AdminAiGuardrailSettings?> GetGuardrailsAsync(Guid tenantId, CancellationToken cancellationToken);
}

public sealed class AdminAiSettingsService : IAdminAiSettingsService
{
    private readonly IAdminAiSettingsRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IAiModelHealthChecker _aiModelHealthChecker;
    private readonly ISemanticSimilarityHealthChecker _semanticSimilarityHealthChecker;

    public AdminAiSettingsService(
        IAdminAiSettingsRepository repository,
        ICurrentUserAccessor currentUser,
        IAiModelHealthChecker aiModelHealthChecker,
        ISemanticSimilarityHealthChecker semanticSimilarityHealthChecker)
    {
        _repository = repository;
        _currentUser = currentUser;
        _aiModelHealthChecker = aiModelHealthChecker;
        _semanticSimilarityHealthChecker = semanticSimilarityHealthChecker;
    }

    public async Task<Result<AdminAiRuntimeResponse>> GetRuntimeAsync(CancellationToken cancellationToken)
    {
        var runtime = await _repository.GetRuntimeAsync(_currentUser.TenantId, cancellationToken);
        return runtime is null
            ? Result<AdminAiRuntimeResponse>.Failure("admin_ai_settings.runtime_missing", "AI runtime settings were not found for this tenant.")
            : Result<AdminAiRuntimeResponse>.Success(runtime);
    }

    public async Task<Result<AdminLlmHealthResponse>> GetLlmHealthAsync(CancellationToken cancellationToken)
    {
        var health = await _aiModelHealthChecker.CheckAsync(cancellationToken);
        return Result<AdminLlmHealthResponse>.Success(
            new AdminLlmHealthResponse(
                health.IsAvailable,
                health.Status,
                health.Message,
                health.Provider,
                health.LlmModel,
                health.OllamaBaseUrl));
    }

    public async Task<Result<AdminSemanticSimilarityHealthResponse>> GetSemanticSimilarityHealthAsync(CancellationToken cancellationToken)
    {
        var health = await _semanticSimilarityHealthChecker.CheckAsync(cancellationToken);
        return Result<AdminSemanticSimilarityHealthResponse>.Success(
            new AdminSemanticSimilarityHealthResponse(
                health.IsAvailable,
                health.Status,
                health.Message,
                health.Provider,
                health.EmbeddingModel,
                health.EmbeddingDimensions,
                health.VectorStore,
                health.OllamaBaseUrl));
    }

    public async Task<Result<AdminAiAgentListResponse>> GetAgentsAsync(CancellationToken cancellationToken)
    {
        var agents = await _repository.ListAgentsAsync(cancellationToken);
        var enabledAgents = agents.Where(agent => agent.Enabled).ToArray();

        return Result<AdminAiAgentListResponse>.Success(new AdminAiAgentListResponse(enabledAgents.Length, enabledAgents));
    }

    public async Task<Result<AdminAiGuardrailsResponse>> GetGuardrailsAsync(CancellationToken cancellationToken)
    {
        var settings = await _repository.GetGuardrailsAsync(_currentUser.TenantId, cancellationToken);
        if (settings is null)
        {
            return Result<AdminAiGuardrailsResponse>.Failure("admin_ai_settings.guardrails_missing", "AI guardrail settings were not found for this tenant.");
        }

        var items = new[]
        {
            new AdminAiGuardrailItem(
                "Human Review",
                settings.HumanReviewRequired ? "Required" : "Optional",
                settings.HumanReviewRequired
                    ? "Tenant policy requires human review before AI-supported decisions are applied."
                    : "Tenant policy does not require human review for advisory AI output."),
            new AdminAiGuardrailItem(
                "Auto Reject",
                settings.AutoRejectEnabled ? "Enabled" : "Disabled",
                settings.AutoRejectEnabled
                    ? "Tenant policy allows automatic candidate rejection."
                    : "Tenant policy prevents AI from rejecting candidates.")
        };

        return Result<AdminAiGuardrailsResponse>.Success(new AdminAiGuardrailsResponse(
            settings.HumanReviewRequired,
            settings.AutoRejectEnabled,
            BuildDecisionBoundary(settings),
            items));
    }

    private static string BuildDecisionBoundary(AdminAiGuardrailSettings settings)
    {
        if (settings.HumanReviewRequired && !settings.AutoRejectEnabled)
        {
            return "AI output is advisory. Human users remain responsible for candidate decisions and workflow movement.";
        }

        return "AI behavior follows the tenant guardrail settings returned by the backend.";
    }
}
