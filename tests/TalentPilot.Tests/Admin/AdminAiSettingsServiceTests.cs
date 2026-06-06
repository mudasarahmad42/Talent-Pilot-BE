using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Admin.AiSettings;

namespace TalentPilot.Tests.Admin;

public sealed class AdminAiSettingsServiceTests
{
    private static readonly Guid TestTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CurrentUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task GetRecentRunsAsync_ReturnsTenantScopedSafeRunLog()
    {
        var repository = new CapturingRepository();
        var service = new AdminAiSettingsService(
            repository,
            new StaticCurrentUserAccessor(),
            new StaticAiModelHealthChecker(),
            new StaticSemanticSimilarityHealthChecker());

        var result = await service.GetRecentRunsAsync(200, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(TestTenantId, repository.LastTenantId);
        Assert.Equal(50, repository.LastCount);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("Applicant Ranking", item.AgentName);
        Assert.Equal("applicant-ranking-json-v2", item.PromptVersion);
        Assert.Equal("Available", item.SemanticSimilarityStatus);
        Assert.True(item.HumanDecisionRequired);
        Assert.Null(item.FailureType);
    }

    [Fact]
    public async Task GetEvaluationAsync_ReturnsJudgeReadyAiMaturityChecklist()
    {
        var service = new AdminAiSettingsService(
            new CapturingRepository(),
            new StaticCurrentUserAccessor(),
            new StaticAiModelHealthChecker(),
            new StaticSemanticSimilarityHealthChecker());

        var result = await service.GetEvaluationAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Demo ready", result.Value.OverallStatus);
        Assert.True(result.Value.ScorePercent >= 90);
        Assert.Contains(result.Value.Items, item => item.Name == "Human oversight" && item.Status == "Passed");
        Assert.Contains(result.Value.Items, item => item.Name == "RAG grounding and citations" && item.Status == "Passed");
        Assert.Contains(result.Value.Items, item => item.Name == "Agent run observability" && item.Status == "Passed");
        Assert.DoesNotContain(result.Value.Items, item => item.Status == "Failed");
    }

    private sealed class CapturingRepository : IAdminAiSettingsRepository
    {
        public Guid LastTenantId { get; private set; }
        public int LastCount { get; private set; }

        public Task<AdminAiRuntimeResponse?> GetRuntimeAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            return Task.FromResult<AdminAiRuntimeResponse?>(new AdminAiRuntimeResponse(
                "Mock/Ollama",
                "llama3.2",
                "nomic-embed-text",
                768,
                "SqlServerVector",
                false));
        }

        public Task<IReadOnlyList<AdminAiAgentDefinition>> ListAgentsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<AdminAiAgentDefinition> agents =
            [
                Agent("job-description-drafter", "Job Description Drafter", "Drafts job descriptions from supplied request evidence.", "Structured job description draft with prompt version metadata."),
                Agent("bench-matching", "Bench Matching", "Ranks internal employees with semantic evidence.", "Structured recommendation list with fit explanations."),
                Agent("applicant-ranking", "Applicant Ranking", "Ranks current applications for recruiter review.", "JSON applicant rankings with explanation and confidence."),
                Agent("interview-question-recommender", "Interview Question Recommender", "Generates interview questions.", "Strict JSON question set with prompt version and rubric."),
                Agent("conversational-rag-assistant", "Conversational RAG Assistant", "Answers workflow questions with retrieved evidence.", "Cited answer with prompt version and citation metadata.")
            ];

            return Task.FromResult(agents);
        }

        public Task<AdminAiGuardrailSettings?> GetGuardrailsAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            return Task.FromResult<AdminAiGuardrailSettings?>(new AdminAiGuardrailSettings(true, false));
        }

        public Task<IReadOnlyList<AdminAiAgentRunListItem>> ListRecentRunsAsync(
            Guid tenantId,
            int count,
            CancellationToken cancellationToken)
        {
            LastTenantId = tenantId;
            LastCount = count;
            IReadOnlyList<AdminAiAgentRunListItem> items =
            [
                new AdminAiAgentRunListItem(
                    Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    "applicant-ranking",
                    "Applicant Ranking",
                    "JobPost",
                    Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    "llama3.2",
                    "nomic-embed-text",
                    "Succeeded",
                    DateTimeOffset.UtcNow.AddSeconds(-8),
                    DateTimeOffset.UtcNow,
                    8000,
                    "Ranked current applicants for recruiter review.",
                    "safe-input-hash",
                    "applicant-ranking-json-v2",
                    "Available",
                    true,
                    null)
            ];

            return Task.FromResult(items);
        }

        private static AdminAiAgentDefinition Agent(
            string id,
            string displayName,
            string responsibility,
            string outputSummary)
        {
            return new AdminAiAgentDefinition(
                id,
                displayName,
                responsibility,
                "Tenant workflow evidence.",
                outputSummary,
                "Advisory only.",
                true);
        }
    }

    private sealed class StaticCurrentUserAccessor : ICurrentUserAccessor
    {
        public Guid UserId => CurrentUserId;

        public Guid TenantId => TestTenantId;

        public string Email => "tenant-admin@example.com";

        public IReadOnlyCollection<string> RoleCodes => Array.Empty<string>();
    }

    private sealed class StaticAiModelHealthChecker : IAiModelHealthChecker
    {
        public Task<AiModelHealth> CheckAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new AiModelHealth(
                true,
                "Available",
                "LLM available.",
                "Mock/Ollama",
                "llama3.2",
                "http://localhost:11434"));
        }
    }

    private sealed class StaticSemanticSimilarityHealthChecker : ISemanticSimilarityHealthChecker
    {
        public Task<SemanticSimilarityHealth> CheckAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticSimilarityHealth(
                true,
                "Available",
                "Embedding service available.",
                "Mock/Ollama",
                "nomic-embed-text",
                768,
                "SqlServerVector",
                "http://localhost:11434"));
        }
    }
}
