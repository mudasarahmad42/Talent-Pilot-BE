using System.Text.Json;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Ai;
using TalentPilot.Application.Operations;

namespace TalentPilot.Tests.Ai;

public sealed class InterviewQuestionRecommendationAgentTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid InterviewId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BankItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task GenerateAsync_UsesLlmAndRetrievedBankItemsForStructuredQuestions()
    {
        var modelProvider = new CapturingModelProvider(CreateValidLlmResponse());
        var vectorStore = new CapturingVectorStore(new Dictionary<Guid, decimal>
        {
            [BankItemId] = 0.91m
        });
        var logger = new CapturingRunLogger();
        var agent = new InterviewQuestionRecommendationAgent(
            modelProvider,
            new StaticEmbeddingProvider(),
            vectorStore,
            new StaticRuntimeSettingsResolver(),
            logger);

        var result = await agent.GenerateAsync(
            TenantId,
            CreateContext(),
            [CreateBankItem()],
            CancellationToken.None);

        Assert.Equal(InterviewQuestionRecommendationAgent.AgentId, modelProvider.LastRequest?.AgentId);
        Assert.Contains("Return strict JSON only", modelProvider.LastRequest?.Prompt);
        Assert.Contains("The top-level value must be one JSON object", modelProvider.LastRequest?.Prompt);
        Assert.Contains("questions: required array with exactly 10 objects", modelProvider.LastRequest?.Prompt);
        Assert.Contains("questions[].followUps must be an array with at least 1 short string", modelProvider.LastRequest?.Prompt);
        Assert.Contains("questions[].evaluationRubric must be an array with at least 2 observable scoring-signal strings", modelProvider.LastRequest?.Prompt);
        Assert.Contains("Allowed values for coverage.roundType, questions[].roundType, and questions[].questionType in this request: Technical", modelProvider.LastRequest?.Prompt);
        Assert.Contains("Do not recommend hiring", modelProvider.LastRequest?.Prompt);
        Assert.Contains($"BankItemId: {BankItemId:D}", modelProvider.LastRequest?.Prompt);
        Assert.Contains("React implementation", modelProvider.LastRequest?.Prompt);
        Assert.Equal("Probe React delivery depth for this technical round.", result.Summary);
        Assert.Equal(10, result.Questions.Count);
        Assert.Equal(BankItemId, result.Questions[0].SourceBankItemId);
        Assert.Equal("React", result.Questions[0].SkillName);
        Assert.True(logger.Succeeded);
        Assert.Equal("Available", result.Coverage.SemanticSimilarityStatus);
        Assert.Contains(vectorStore.UpsertedRecords, record =>
            record.EntityType == "InterviewQuestionBankItem" &&
            record.EntityId == BankItemId &&
            record.SourceType == "InterviewQuestionBankItemText");
    }

    [Fact]
    public async Task GenerateAsync_WhenLlmReturnsInvalidJson_FailsClosed()
    {
        var modelProvider = new CapturingModelProvider("not json");
        var logger = new CapturingRunLogger();
        var agent = new InterviewQuestionRecommendationAgent(
            modelProvider,
            new StaticEmbeddingProvider(),
            new CapturingVectorStore(new Dictionary<Guid, decimal>
            {
                [BankItemId] = 0.88m
            }),
            new StaticRuntimeSettingsResolver(),
            logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.GenerateAsync(
            TenantId,
            CreateContext(),
            [CreateBankItem()],
            CancellationToken.None));

        Assert.Equal(2, modelProvider.CallCount);
        Assert.Contains("did not parse as the required Talent Pilot interview-question JSON contract", modelProvider.Requests[1].Prompt);
        Assert.Contains("questions must be an array with exactly 10 objects", modelProvider.Requests[1].Prompt);
        Assert.Contains("Each evaluationRubric value must be a non-empty array with at least 2 strings", modelProvider.Requests[1].Prompt);
        Assert.False(logger.Succeeded);
        Assert.True(logger.Failed);
    }

    [Fact]
    public async Task GenerateAsync_WhenScreeningRoundReceivesTechnicalQuestions_FailsClosed()
    {
        var modelProvider = new CapturingModelProvider(CreateValidLlmResponse());
        var logger = new CapturingRunLogger();
        var agent = new InterviewQuestionRecommendationAgent(
            modelProvider,
            new StaticEmbeddingProvider(),
            new CapturingVectorStore(new Dictionary<Guid, decimal>
            {
                [BankItemId] = 0.88m
            }),
            new StaticRuntimeSettingsResolver(),
            logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.GenerateAsync(
            TenantId,
            CreateContext("HR Screening", "Screening"),
            [CreateBankItem()],
            CancellationToken.None));

        Assert.Equal(2, modelProvider.CallCount);
        Assert.Contains("this is an HR/screening interview", modelProvider.Requests[0].Prompt);
        Assert.False(logger.Succeeded);
        Assert.True(logger.Failed);
    }

    [Fact]
    public async Task GenerateAsync_WhenLlmOmitsFollowUpsAndRubric_UsesSafeDefaults()
    {
        var modelProvider = new CapturingModelProvider(CreateScreeningResponseWithoutFollowUpsOrRubric());
        var logger = new CapturingRunLogger();
        var agent = new InterviewQuestionRecommendationAgent(
            modelProvider,
            new StaticEmbeddingProvider(),
            new CapturingVectorStore(new Dictionary<Guid, decimal>
            {
                [BankItemId] = 0.88m
            }),
            new StaticRuntimeSettingsResolver(),
            logger);

        var result = await agent.GenerateAsync(
            TenantId,
            CreateContext("HR Screening", "Screening"),
            [CreateBankItem("Screening")],
            CancellationToken.None);

        Assert.Equal(10, result.Questions.Count);
        Assert.All(result.Questions, question =>
        {
            Assert.NotEmpty(question.FollowUps);
            Assert.True(question.EvaluationRubric.Count >= 2);
            Assert.Contains(question.EvaluationRubric, item => item.Contains("Strong answer demonstrates", StringComparison.OrdinalIgnoreCase));
        });
        Assert.True(logger.Succeeded);
    }

    private static OperationsInterviewQuestionRecommendationContext CreateContext(
        string roundName = "Technical Interview",
        string roundType = "Technical")
    {
        return new OperationsInterviewQuestionRecommendationContext(
            InterviewId,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            "TP-REQ-300",
            "Senior React Developer",
            "Relia",
            "Engineering",
            "Lahore",
            roundName,
            roundType,
            45,
            "Scheduled",
            DateTimeOffset.UtcNow.AddHours(2),
            "Bilal Hassan",
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            "Ayesha Khan",
            "ayesha@example.com",
            "Frontend Engineer",
            "Product Studio",
            6.5m,
            30,
            "Interviewing",
            "I have delivered React portals.",
            "Prioritize maintainability and customer-facing delivery.",
            """{"portfolio":"React dashboard"}""",
            "Build customer portal features with React and TypeScript.",
            "Senior React role requiring component architecture and maintainable frontend delivery.",
            5,
            8,
            [new OperationsInterviewQuestionSkill(Guid.Parse("99999999-9999-9999-9999-999999999999"), "React", "Frontend Engineer")],
            [new OperationsInterviewQuestionSkill(Guid.Parse("99999999-9999-9999-9999-999999999999"), "React", "Frontend Engineer")],
            [
                new OperationsApplicantDocumentEvidence(
                    Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                    "CV",
                    "resume.docx",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    4096,
                    "Local",
                    "resume.docx",
                    null,
                    "hash-1",
                    DateTimeOffset.UtcNow.AddDays(-1),
                    "Extracted",
                    true,
                    "React implementation and UI maintainability evidence.",
                    "text-hash-1",
                    "docx-wordprocessingml-v1",
                    DateTimeOffset.UtcNow.AddDays(-1),
                    null)
            ],
            []);
    }

    private static InterviewQuestionBankItem CreateBankItem(string roundType = "Technical")
    {
        return new InterviewQuestionBankItem(
            BankItemId,
            TenantId,
            Guid.Parse("99999999-9999-9999-9999-999999999999"),
            "React",
            "Frontend Engineer",
            null,
            "Frontend Engineer",
            roundType,
            "Intermediate",
            "Walk through a React implementation and the trade-offs you made.",
            "Candidate demonstrates React delivery depth and maintainability judgment.",
            ["What changed after launch?"],
            ["Specific React evidence", "Trade-off quality"],
            "Talent Pilot generated interview question seed corpus v1",
            null,
            "hash");
    }

    private static string CreateValidLlmResponse()
    {
        return JsonSerializer.Serialize(new
        {
            summary = "Probe React delivery depth for this technical round.",
            rationale = "The role needs frontend maintainability evidence.",
            coverage = new
            {
                roundType = "Technical",
                targetQuestionCount = 10,
                skillsCovered = new[] { "React" },
                candidateEvidenceUsed = new[] { "Job post", "CV" }
            },
            questions = Enumerable.Range(1, 10).Select(index => new
            {
                questionText = $"How did you keep the React UI maintainable as requirements changed in project {index}?",
                questionType = "Technical",
                roundType = "Technical",
                skillName = "React",
                difficulty = "Intermediate",
                rationale = "Validates frontend maintainability judgment.",
                expectedSignal = "Candidate names component boundaries, state management, tests, and trade-offs.",
                followUps = new[] { "What trade-off did you make?" },
                evaluationRubric = new[] { "Specific project evidence", "Clear trade-off" },
                sourceBankItemId = BankItemId.ToString("D")
            })
        });
    }

    private static string CreateScreeningResponseWithoutFollowUpsOrRubric()
    {
        return JsonSerializer.Serialize(new
        {
            summary = "Probe candidate motivation, communication, and role alignment for HR screening.",
            rationale = "The HR screening round should validate logistics and role fit.",
            coverage = new
            {
                roundType = "Screening",
                targetQuestionCount = 10,
                skillsCovered = new[] { "React", "Role alignment" },
                candidateEvidenceUsed = new[] { "Job post", "CV", "Application" }
            },
            questions = Enumerable.Range(1, 10).Select(index => new
            {
                questionText = $"What makes this Senior React Developer opportunity a good fit for you at this stage {index}?",
                questionType = "Screening",
                roundType = "Screening",
                skillName = "Role alignment",
                difficulty = "Basic",
                rationale = "Validates motivation and communication for HR screening.",
                expectedSignal = "Candidate gives concrete motivation, communication, and availability evidence.",
                followUps = Array.Empty<string>(),
                evaluationRubric = Array.Empty<string>(),
                sourceBankItemId = BankItemId.ToString("D")
            })
        });
    }

    private sealed class CapturingModelProvider : IAiModelProvider
    {
        private readonly string _response;

        public CapturingModelProvider(string response)
        {
            _response = response;
        }

        public AiPromptRequest? LastRequest { get; private set; }
        public List<AiPromptRequest> Requests { get; } = [];
        public int CallCount { get; private set; }

        public Task<string> GenerateAsync(AiPromptRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            Requests.Add(request);
            CallCount++;
            return Task.FromResult(_response);
        }
    }

    private sealed class StaticEmbeddingProvider : IEmbeddingProvider
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Repeat(0.75f, 768).ToArray());
        }
    }

    private sealed class CapturingVectorStore : IVectorStore
    {
        private readonly IReadOnlyDictionary<Guid, decimal> _scores;

        public CapturingVectorStore(IReadOnlyDictionary<Guid, decimal> scores)
        {
            _scores = scores;
        }

        public List<VectorRecord> UpsertedRecords { get; } = [];

        public Task UpsertAsync(VectorRecord record, CancellationToken cancellationToken)
        {
            UpsertedRecords.Add(record);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken)
        {
            IReadOnlyList<VectorSearchResult> results = _scores
                .Select(item => new VectorSearchResult(item.Key, item.Value, "test score"))
                .ToArray();
            return Task.FromResult(results);
        }

        public Task<string?> GetActiveSourceTextHashAsync(
            Guid tenantId,
            string entityType,
            Guid entityId,
            string sourceType,
            string embeddingModel,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class StaticRuntimeSettingsResolver : IAiRuntimeSettingsResolver
    {
        public Task<AiRuntimeSettingsSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new AiRuntimeSettingsSnapshot(
                TenantId,
                "Mock/Ollama",
                "llama3.2",
                "nomic-embed-text",
                768,
                "SqlServerVector",
                "http://localhost:11434"));
        }
    }

    private sealed class CapturingRunLogger : IAiAgentRunLogger
    {
        public bool Succeeded { get; private set; }
        public bool Failed { get; private set; }

        public Task<Guid> StartAsync(AiAgentRunStart run, CancellationToken cancellationToken)
        {
            return Task.FromResult(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        }

        public Task SucceedAsync(
            Guid tenantId,
            Guid runId,
            string outputSummary,
            IReadOnlyDictionary<string, string> metadata,
            CancellationToken cancellationToken)
        {
            Succeeded = true;
            return Task.CompletedTask;
        }

        public Task FailAsync(
            Guid tenantId,
            Guid runId,
            string outputSummary,
            IReadOnlyDictionary<string, string> metadata,
            CancellationToken cancellationToken)
        {
            Failed = true;
            return Task.CompletedTask;
        }
    }
}
