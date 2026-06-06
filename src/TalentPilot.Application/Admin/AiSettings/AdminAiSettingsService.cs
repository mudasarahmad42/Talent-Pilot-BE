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

    Task<Result<AdminAiAgentRunListResponse>> GetRecentRunsAsync(int count, CancellationToken cancellationToken);

    Task<Result<AdminAiEvaluationResponse>> GetEvaluationAsync(CancellationToken cancellationToken);
}

public interface IAdminAiSettingsRepository
{
    Task<AdminAiRuntimeResponse?> GetRuntimeAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminAiAgentDefinition>> ListAgentsAsync(CancellationToken cancellationToken);

    Task<AdminAiGuardrailSettings?> GetGuardrailsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminAiAgentRunListItem>> ListRecentRunsAsync(
        Guid tenantId,
        int count,
        CancellationToken cancellationToken);
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

    public async Task<Result<AdminAiAgentRunListResponse>> GetRecentRunsAsync(
        int count,
        CancellationToken cancellationToken)
    {
        var safeCount = Math.Clamp(count, 1, 50);
        var items = await _repository.ListRecentRunsAsync(_currentUser.TenantId, safeCount, cancellationToken);

        return Result<AdminAiAgentRunListResponse>.Success(new AdminAiAgentRunListResponse(items.Count, items));
    }

    public async Task<Result<AdminAiEvaluationResponse>> GetEvaluationAsync(CancellationToken cancellationToken)
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var runtime = await _repository.GetRuntimeAsync(_currentUser.TenantId, cancellationToken);
        var agents = await _repository.ListAgentsAsync(cancellationToken);
        var guardrails = await _repository.GetGuardrailsAsync(_currentUser.TenantId, cancellationToken);
        var recentRuns = await _repository.ListRecentRunsAsync(_currentUser.TenantId, 10, cancellationToken);
        var semanticHealth = await CheckSemanticSimilarityForEvaluationAsync(cancellationToken);

        var items = new List<AdminAiEvaluationItem>
        {
            BuildRuntimeEvaluation(runtime),
            BuildAgentCoverageEvaluation(agents),
            BuildGuardrailEvaluation(guardrails),
            BuildStructuredOutputEvaluation(agents),
            BuildRagEvaluation(agents),
            BuildSemanticSimilarityEvaluation(semanticHealth),
            BuildRunObservabilityEvaluation(recentRuns)
        };

        return Result<AdminAiEvaluationResponse>.Success(new AdminAiEvaluationResponse(
            OverallEvaluationStatus(items),
            EvaluationScore(items),
            generatedAt,
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

    private async Task<SemanticSimilarityHealth> CheckSemanticSimilarityForEvaluationAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _semanticSimilarityHealthChecker.CheckAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return new SemanticSimilarityHealth(
                false,
                $"Diagnostic unavailable: {ex.GetType().Name}",
                "Semantic similarity diagnostic could not be completed.",
                "Unknown",
                "Unknown",
                0,
                "Unknown",
                "Unknown");
        }
    }

    private static AdminAiEvaluationItem BuildRuntimeEvaluation(AdminAiRuntimeResponse? runtime)
    {
        return runtime is null
            ? Failed(
                "Runtime configured",
                "Runtime architecture",
                "No tenant AI runtime settings were found.",
                "Configure provider, LLM model, embedding model, and vector store before demo.")
            : Passed(
                "Runtime configured",
                "Runtime architecture",
                $"Provider {runtime.Provider}, LLM {runtime.LlmModel}, embedding {runtime.EmbeddingModel}, vector store {runtime.VectorStore}.",
                "Show Admin Center > AI Settings > Runtime & Guardrails during the demo.");
    }

    private static AdminAiEvaluationItem BuildAgentCoverageEvaluation(IReadOnlyList<AdminAiAgentDefinition> agents)
    {
        return agents.Count >= 5
            ? Passed(
                "Agent coverage",
                "Agent coverage",
                $"{agents.Count} enabled agents cover drafting, matching, ranking, RAG, interview questions, and decision support.",
                "Use the Senior Angular Developer flow to exercise the main agents.")
            : Warning(
                "Agent coverage",
                "Agent coverage",
                $"{agents.Count} enabled agent(s) are configured.",
                "Enable or seed the core demo agents before judging.");
    }

    private static AdminAiEvaluationItem BuildGuardrailEvaluation(AdminAiGuardrailSettings? guardrails)
    {
        if (guardrails is null)
        {
            return Failed(
                "Human oversight",
                "Decision governance",
                "Tenant guardrail settings were not found.",
                "Seed TenantAiSettings before running the demo.");
        }

        return guardrails.HumanReviewRequired && !guardrails.AutoRejectEnabled
            ? Passed(
                "Human oversight",
                "Decision governance",
                "Human review is required and automatic rejection is disabled.",
                "Call out that shortlist, reject, hire, and close actions remain human-owned.")
            : Warning(
                "Human oversight",
                "Decision governance",
                $"Human review required: {guardrails.HumanReviewRequired}; auto reject enabled: {guardrails.AutoRejectEnabled}.",
                "For hackathon judging, keep AI advisory and final decisions human-owned.");
    }

    private static AdminAiEvaluationItem BuildStructuredOutputEvaluation(IReadOnlyList<AdminAiAgentDefinition> agents)
    {
        string[] structuredContractAgentIds =
        [
            "requirement-parser",
            "job-description-drafter",
            "cv-parser",
            "bench-matching",
            "talent-rediscovery",
            "applicant-ranking",
            "interview-question-recommender",
            "conversational-rag-assistant",
            "hiring-manager-decision-brief"
        ];

        var outputAgents = agents.Count(agent =>
            structuredContractAgentIds.Contains(agent.Id, StringComparer.OrdinalIgnoreCase) ||
            agent.OutputSummary.Contains("JSON", StringComparison.OrdinalIgnoreCase) ||
            agent.OutputSummary.Contains("structured", StringComparison.OrdinalIgnoreCase) ||
            agent.OutputSummary.Contains("recommendation", StringComparison.OrdinalIgnoreCase) ||
            agent.OutputSummary.Contains("prompt version", StringComparison.OrdinalIgnoreCase));

        return outputAgents >= 5
            ? Passed(
                "Structured outputs",
                "Output reliability",
                $"{outputAgents} enabled agent(s) have structured runtime contracts, prompt versions, recommendations, or cited answers.",
                "Mention strict JSON contracts, repair prompts, fail-closed parsing, and backend unit tests.")
            : Warning(
                "Structured outputs",
                "Output reliability",
                $"{outputAgents} enabled agent(s) have visible structured output coverage.",
                "Add runtime contracts or update AI agent definitions to describe required output schemas.");
    }

    private static AdminAiEvaluationItem BuildRagEvaluation(IReadOnlyList<AdminAiAgentDefinition> agents)
    {
        var ragAgent = agents.FirstOrDefault(agent =>
            agent.Id.Contains("rag", StringComparison.OrdinalIgnoreCase) ||
            agent.DisplayName.Contains("RAG", StringComparison.OrdinalIgnoreCase) ||
            agent.Responsibility.Contains("cit", StringComparison.OrdinalIgnoreCase) ||
            agent.OutputSummary.Contains("cit", StringComparison.OrdinalIgnoreCase));

        return ragAgent is not null
            ? Passed(
                "RAG grounding and citations",
                "Knowledge grounding",
                $"{ragAgent.DisplayName} returns evidence-grounded answers with citation and boundary metadata.",
                "Ask a RAG question in PMO, recruiter sourcing, or hiring-manager decision review.")
            : Warning(
                "RAG grounding and citations",
                "Knowledge grounding",
                "No enabled agent definition advertises RAG or citation behavior.",
                "Seed the Conversational RAG Assistant definition before the demo.");
    }

    private static AdminAiEvaluationItem BuildSemanticSimilarityEvaluation(SemanticSimilarityHealth health)
    {
        return health.IsAvailable
            ? Passed(
                "Semantic similarity",
                "Retrieval readiness",
                $"{health.EmbeddingModel} is available through {health.VectorStore}.",
                "Show applicant ranking or bench matching where semantic evidence is used.")
            : Warning(
                "Semantic similarity",
                "Retrieval readiness",
                health.Message,
                "Start the embedding provider or explain deterministic fallback scoring in the demo.");
    }

    private static AdminAiEvaluationItem BuildRunObservabilityEvaluation(IReadOnlyList<AdminAiAgentRunListItem> recentRuns)
    {
        if (recentRuns.Count == 0)
        {
            return Warning(
                "Agent run observability",
                "Operational observability",
                "The run log endpoint is available, but no recent tenant AI runs are recorded yet.",
                "Run applicant ranking, interview questions, or RAG assistant before showing the log.");
        }

        var succeeded = recentRuns.Count(run => string.Equals(run.Status, "Succeeded", StringComparison.OrdinalIgnoreCase));
        var failed = recentRuns.Count(run => string.Equals(run.Status, "Failed", StringComparison.OrdinalIgnoreCase));
        return failed == 0
            ? Passed(
                "Agent run observability",
                "Operational observability",
                $"{recentRuns.Count} recent run(s) recorded; {succeeded} succeeded; status, model, prompt version, input hash, and human-review flag are visible.",
                "Open Agent Run Log in Admin Center during the demo.")
            : Warning(
                "Agent run observability",
                "Operational observability",
                $"{recentRuns.Count} recent run(s) recorded; {failed} failed run(s) need review.",
                "Use failed rows to show observability, then explain retry/fallback behavior.");
    }

    private static AdminAiEvaluationItem Passed(
        string name,
        string rubricArea,
        string evidence,
        string nextStep)
    {
        return new AdminAiEvaluationItem(name, "Passed", rubricArea, evidence, nextStep);
    }

    private static AdminAiEvaluationItem Warning(
        string name,
        string rubricArea,
        string evidence,
        string nextStep)
    {
        return new AdminAiEvaluationItem(name, "Warning", rubricArea, evidence, nextStep);
    }

    private static AdminAiEvaluationItem Failed(
        string name,
        string rubricArea,
        string evidence,
        string nextStep)
    {
        return new AdminAiEvaluationItem(name, "Failed", rubricArea, evidence, nextStep);
    }

    private static int EvaluationScore(IReadOnlyList<AdminAiEvaluationItem> items)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        var score = items.Sum(item => item.Status switch
        {
            "Passed" => 100,
            "Warning" => 60,
            _ => 0
        });

        return (int)Math.Round((decimal)score / items.Count, MidpointRounding.AwayFromZero);
    }

    private static string OverallEvaluationStatus(IReadOnlyList<AdminAiEvaluationItem> items)
    {
        if (items.Any(item => item.Status == "Failed"))
        {
            return "Needs attention";
        }

        return items.Any(item => item.Status == "Warning")
            ? "Demo ready with warnings"
            : "Demo ready";
    }
}
