using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Ai;
using TalentPilot.Application.Operations;

namespace TalentPilot.Tests.Ai;

public sealed class ApplicantRankingAgentTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid JobRequestId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid JobPostId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StrongApplicationId = Guid.Parse("33333333-3333-3333-3333-333333333331");
    private static readonly Guid WeakApplicationId = Guid.Parse("33333333-3333-3333-3333-333333333332");

    [Fact]
    public async Task RankAsync_RanksCurrentApplicationsAndUsesCandidateOnlyEvidence()
    {
        var modelProvider = new CapturingModelProvider($$"""
            [
              {
                "jobApplicationId": "{{StrongApplicationId:D}}",
                "explanation": "Ayesha is a strong applicant because her cover letter, CV, and React/Azure profile match the current job post. Recruiter remains responsible for the decision."
              },
              {
                "jobApplicationId": "{{WeakApplicationId:D}}",
                "explanation": "Omar has partial React evidence but the LLM notes the missing Azure evidence and lower experience alignment for recruiter review."
              }
            ]
            """);
        var vectorStore = new CapturingVectorStore(new Dictionary<Guid, decimal>
        {
            [StrongApplicationId] = 0.92m,
            [WeakApplicationId] = 0.10m
        });
        var logger = new CapturingRunLogger();
        var agent = new ApplicantRankingAgent(
            modelProvider,
            new StaticEmbeddingProvider(),
            vectorStore,
            new StaticRuntimeSettingsResolver(),
            logger);

        var result = await agent.RankAsync(TenantId, CreateContext(), CancellationToken.None);

        Assert.Equal(ApplicantRankingAgent.AgentId, modelProvider.LastRequest?.AgentId);
        Assert.Contains("Do not search the web", modelProvider.LastRequest?.Prompt);
        Assert.Contains("Do not decide whether to shortlist", modelProvider.LastRequest?.Prompt);
        Assert.Contains("Cover letter", modelProvider.LastRequest?.Prompt);
        Assert.Contains("React Azure delivery", modelProvider.LastRequest?.Prompt);
        Assert.Equal(StrongApplicationId, result.Matches[0].JobApplicationId);
        Assert.True(result.Matches[0].Score > result.Matches[1].Score);
        Assert.Contains("cover letter", result.Matches[0].Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cover letter submitted.", result.Matches[0].DocumentEvidence);
        Assert.Contains(result.Matches[0].DocumentEvidence, item => item.Contains("resume.docx", StringComparison.OrdinalIgnoreCase));
        Assert.True(logger.Succeeded);
        Assert.Equal("Available", logger.Metadata["semanticSimilarityStatus"]);
        Assert.Equal(3, vectorStore.UpsertedRecords.Count);
        Assert.Contains(vectorStore.UpsertedRecords, record =>
            record.EntityType == "JobPost" &&
            record.EntityId == JobPostId &&
            record.SourceType == "ApplicantRankingRequirementProfile");
        Assert.Equal(2, vectorStore.UpsertedRecords.Count(record =>
            record.EntityType == "JobApplication" &&
            record.SourceType == "JobApplicationEvidenceProfile"));
        Assert.DoesNotContain(vectorStore.UpsertedRecords, record => record.EntityType == "Employee");
    }

    [Fact]
    public async Task RankAsync_WhenEmbeddingServiceIsUnavailable_RecordsActionableSemanticStatus()
    {
        var modelProvider = new CapturingModelProvider(CreateExplanationResponse(StrongApplicationId, WeakApplicationId));
        var logger = new CapturingRunLogger();
        var agent = new ApplicantRankingAgent(
            modelProvider,
            new ThrowingEmbeddingProvider(new InvalidOperationException("No connection could be made because the target machine actively refused it. (localhost:11434)")),
            new CapturingVectorStore(new Dictionary<Guid, decimal>()),
            new StaticRuntimeSettingsResolver(),
            logger);

        var result = await agent.RankAsync(TenantId, CreateContext(), CancellationToken.None);

        Assert.Equal(2, result.Matches.Count);
        Assert.True(logger.Succeeded);
        Assert.Contains("Ollama embedding service is not reachable", logger.Metadata["semanticSimilarityStatus"]);
        Assert.Contains("nomic-embed-text", logger.Metadata["semanticSimilarityStatus"]);
        Assert.DoesNotContain("actively refused", logger.Metadata["semanticSimilarityStatus"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RankAsync_AcceptsObjectWrappedExplanationResponses()
    {
        var modelProvider = new CapturingModelProvider($$"""
            {
              "explanations": [
                {
                  "jobApplicationId": "{{StrongApplicationId:D}}",
                  "rationale": "Wrapped response still identifies Ayesha as the strongest React and Azure applicant."
                },
                {
                  "jobApplicationId": "{{WeakApplicationId:D}}",
                  "rationale": "Wrapped response notes Omar has partial React evidence and missing Azure evidence."
                }
              ]
            }
            """);
        var logger = new CapturingRunLogger();
        var agent = new ApplicantRankingAgent(
            modelProvider,
            new StaticEmbeddingProvider(),
            new CapturingVectorStore(new Dictionary<Guid, decimal>()),
            new StaticRuntimeSettingsResolver(),
            logger);

        var result = await agent.RankAsync(TenantId, CreateContext(), CancellationToken.None);

        Assert.Equal(2, result.Matches.Count);
        Assert.Contains("Wrapped response", result.Matches[0].Explanation);
        Assert.True(logger.Succeeded);
    }

    [Fact]
    public async Task RankAsync_WhenModelDoesNotReturnExplanations_UsesDeterministicFallback()
    {
        var logger = new CapturingRunLogger();
        var agent = new ApplicantRankingAgent(
            new CapturingModelProvider("[]"),
            new StaticEmbeddingProvider(),
            new CapturingVectorStore(new Dictionary<Guid, decimal>()),
            new StaticRuntimeSettingsResolver(),
            logger);

        var result = await agent.RankAsync(TenantId, CreateContext(), CancellationToken.None);

        Assert.Equal(2, result.Matches.Count);
        Assert.True(logger.Succeeded);
        Assert.False(logger.Failed);
        Assert.Contains("deterministic ranking", result.Matches[0].Explanation);
        Assert.Contains("Recruiter review is still required", result.Matches[0].Explanation);
    }

    private static OperationsApplicantRankingContext CreateContext()
    {
        var jobRequest = new OperationsJobRequest(
            JobRequestId,
            "TP-REQ-300",
            "Senior React Developer",
            "Relia",
            "Customer portal and Azure delivery context for applicant ranking.",
            "Build React customer portal features with Azure deployment exposure.",
            "Engineering",
            ["React", "Azure"],
            "5-8 years",
            "Lahore",
            1,
            0,
            "High",
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            "Recruiter Sourcing",
            null,
            "Sara Malik",
            "Published",
            DateTimeOffset.UtcNow);
        var jobPost = new OperationsJobPost(
            JobPostId,
            JobRequestId,
            "Senior React Developer",
            "React and Azure portal role.",
            "Engineering",
            "Lahore",
            5,
            8,
            1,
            "Published",
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            "Sara Malik",
            DateTimeOffset.UtcNow.AddDays(-2),
            null,
            DateTimeOffset.UtcNow.AddDays(-3),
            DateTimeOffset.UtcNow.AddDays(-1),
            [
                new OperationsJobPostSkill(Guid.NewGuid(), "React", "Frontend"),
                new OperationsJobPostSkill(Guid.NewGuid(), "Azure", "Cloud")
            ],
            []);

        return new OperationsApplicantRankingContext(
            jobRequest,
            jobPost,
            ["React", "Azure"],
            5,
            8,
            [
                CreateApplication(
                    StrongApplicationId,
                    "Ayesha Khan",
                    6.5m,
                    ["React", "Azure"],
                    [],
                    "I have delivered React Azure portals and can join quickly.",
                    [
                        new OperationsApplicantDocumentEvidence(
                            Guid.NewGuid(),
                            "CV",
                            "resume.docx",
                            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                            2048,
                            "Local",
                            "resume.docx",
                            null,
                            "hash-1",
                            DateTimeOffset.UtcNow.AddDays(-1),
                            "Extracted",
                            true,
                            "React Azure delivery and portal engineering experience.",
                            "text-hash-1",
                            "docx-wordprocessingml-v1",
                            DateTimeOffset.UtcNow.AddDays(-1),
                            null)
                    ]),
                CreateApplication(
                    WeakApplicationId,
                    "Omar Sheikh",
                    3.5m,
                    ["React"],
                    ["Azure"],
                    null,
                    [])
            ]);
    }

    private static string CreateExplanationResponse(params Guid[] jobApplicationIds)
    {
        return "[" + string.Join(",", jobApplicationIds.Select((id, index) =>
            $@"{{""jobApplicationId"":""{id:D}"",""explanation"":""LLM explanation {index + 1} for recruiter review.""}}")) + "]";
    }

    private static OperationsApplicantRankingApplication CreateApplication(
        Guid jobApplicationId,
        string candidateName,
        decimal experienceYears,
        IReadOnlyList<string> matchedSkills,
        IReadOnlyList<string> missingSkills,
        string? coverLetter,
        IReadOnlyList<OperationsApplicantDocumentEvidence> documents)
    {
        var candidateId = Guid.NewGuid();
        return new OperationsApplicantRankingApplication(
            jobApplicationId,
            candidateId,
            candidateName,
            $"{candidateName.Replace(" ", ".", StringComparison.OrdinalIgnoreCase).ToLowerInvariant()}@example.com",
            "Active",
            "Senior React Developer",
            "Product Studio",
            experienceYears,
            15,
            "Applied",
            "JobPortal",
            null,
            coverLetter,
            DateTimeOffset.UtcNow.AddDays(-1),
            """{"location":"Lahore"}""",
            matchedSkills.Concat(missingSkills).ToArray(),
            matchedSkills,
            missingSkills,
            documents,
            [],
            []);
    }

    private sealed class CapturingModelProvider : IAiModelProvider
    {
        private readonly string _response;

        public CapturingModelProvider(string response)
        {
            _response = response;
        }

        public AiPromptRequest? LastRequest { get; private set; }

        public Task<string> GenerateAsync(AiPromptRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }

    private sealed class StaticEmbeddingProvider : IEmbeddingProvider
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
        {
            var value = text.Contains("Ayesha", StringComparison.OrdinalIgnoreCase) ? 0.9f : 0.2f;
            return Task.FromResult(Enumerable.Repeat(value, 768).ToArray());
        }
    }

    private sealed class ThrowingEmbeddingProvider : IEmbeddingProvider
    {
        private readonly Exception _exception;

        public ThrowingEmbeddingProvider(Exception exception)
        {
            _exception = exception;
        }

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
        {
            throw _exception;
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
        public string OutputSummary { get; private set; } = string.Empty;
        public IReadOnlyDictionary<string, string> Metadata { get; private set; } =
            new Dictionary<string, string>();

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
            OutputSummary = outputSummary;
            Metadata = metadata;
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
            OutputSummary = outputSummary;
            return Task.CompletedTask;
        }
    }

}
