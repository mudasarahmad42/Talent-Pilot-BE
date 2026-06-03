using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Ai;
using TalentPilot.Application.Operations;

namespace TalentPilot.Tests.Ai;

public sealed class TalentRediscoveryAgentTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid JobRequestId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StrongCandidateId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SecondaryCandidateId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task RankAsync_RanksWarmCandidatesAndUsesNoWebSearchGuardrail()
    {
        var modelProvider = new CapturingModelProvider($$"""
            [
              {
                "candidateId": "{{StrongCandidateId:D}}",
                "explanation": "Nida is a strong warm candidate because prior React portal feedback was positive and the current skills overlap is high. Recruiter should still validate availability before outreach."
              }
            ]
            """);
        var vectorStore = new CapturingVectorStore(new Dictionary<Guid, decimal>
        {
            [StrongCandidateId] = 0.90m,
            [SecondaryCandidateId] = 0.20m
        });
        var logger = new CapturingRunLogger();
        var agent = new TalentRediscoveryAgent(
            modelProvider,
            new StaticEmbeddingProvider(),
            vectorStore,
            new StaticRuntimeSettingsResolver(),
            logger);

        var result = await agent.RankAsync(TenantId, CreateContext(), CancellationToken.None);

        Assert.Equal(TalentRediscoveryAgent.AgentId, modelProvider.LastRequest?.AgentId);
        Assert.Contains("Do not search the web", modelProvider.LastRequest?.Prompt);
        Assert.Contains("Do not decide whether to contact", modelProvider.LastRequest?.Prompt);
        Assert.Equal(StrongCandidateId, result.Matches[0].CandidateId);
        Assert.True(result.Matches[0].Score > result.Matches[1].Score);
        Assert.Contains("current skills overlap is high", result.Matches[0].Explanation);
        Assert.True(logger.Succeeded);
        Assert.Equal(3, vectorStore.UpsertedRecords.Count);
        Assert.Equal(2, vectorStore.UpsertedRecords.Count(record => record.EntityType == "Candidate"));
        Assert.Contains(vectorStore.UpsertedRecords, record =>
            record.EntityType == "JobRequest" &&
            record.EntityId == JobRequestId &&
            record.SourceType == "TalentRediscoveryRequirementProfile");
        Assert.All(vectorStore.UpsertedRecords.Where(record => record.EntityType == "Candidate"), record =>
        {
            Assert.Equal("Candidate", record.EntityType);
            Assert.Equal("CandidateProfile", record.SourceType);
        });
        Assert.DoesNotContain(vectorStore.UpsertedRecords, record => record.EntityType == "Employee");
        Assert.Equal("Available", logger.Metadata["semanticSimilarityStatus"]);
    }

    [Fact]
    public async Task RankAsync_PrioritizesOnHoldCandidateWhoClearedAllInterviews()
    {
        var onHoldCandidateId = Guid.Parse("22222222-2222-2222-2222-222222222223");
        var halfPassedCandidateId = Guid.Parse("33333333-3333-3333-3333-333333333334");
        var context = CreatePriorityContext(
            CreateCandidate(
                onHoldCandidateId,
                "On Hold Candidate",
                "OnHold",
                [
                    CreateInterview(onHoldCandidateId, "Completed", "Proceed", 4, 4, 4),
                    CreateInterview(onHoldCandidateId, "Completed", "Hire", 4, 5, 4)
                ]),
            CreateCandidate(
                halfPassedCandidateId,
                "Half Passed Candidate",
                "Rejected",
                [
                    CreateInterview(halfPassedCandidateId, "Completed", "Proceed", 4, 4, 4),
                    CreateInterview(halfPassedCandidateId, "Completed", "NoHire", 2, 3, 2)
                ]));
        var agent = CreateAgent(new CapturingVectorStore(new Dictionary<Guid, decimal>
        {
            [onHoldCandidateId] = 0.6m,
            [halfPassedCandidateId] = 0.6m
        }));

        var result = await agent.RankAsync(TenantId, context, CancellationToken.None);

        Assert.Equal(onHoldCandidateId, result.Matches[0].CandidateId);
        Assert.True(result.Matches[0].Score > result.Matches[1].Score);
    }

    [Fact]
    public async Task RankAsync_PrioritizesHalfPassedCandidateAboveWeakHistory()
    {
        var halfPassedCandidateId = Guid.Parse("33333333-3333-3333-3333-333333333335");
        var weakCandidateId = Guid.Parse("33333333-3333-3333-3333-333333333336");
        var context = CreatePriorityContext(
            CreateCandidate(
                halfPassedCandidateId,
                "Half Passed Candidate",
                "Rejected",
                [
                    CreateInterview(halfPassedCandidateId, "Completed", "Proceed", 4, 4, 4),
                    CreateInterview(halfPassedCandidateId, "Completed", "NoHire", 2, 3, 2)
                ]),
            CreateCandidate(
                weakCandidateId,
                "Weak History Candidate",
                "Rejected",
                []));
        var agent = CreateAgent(new CapturingVectorStore(new Dictionary<Guid, decimal>
        {
            [halfPassedCandidateId] = 0.4m,
            [weakCandidateId] = 0.4m
        }));

        var result = await agent.RankAsync(TenantId, context, CancellationToken.None);

        Assert.Equal(halfPassedCandidateId, result.Matches[0].CandidateId);
    }

    [Fact]
    public async Task RankAsync_DoesNotLetUnrelatedOnHoldBackendHistoryBeatCurrentSkillMatch()
    {
        var backendOnHoldCandidateId = Guid.Parse("33333333-3333-3333-3333-333333333337");
        var reactCandidateId = Guid.Parse("33333333-3333-3333-3333-333333333338");
        var context = CreatePriorityContext(
            CreateCandidate(
                backendOnHoldCandidateId,
                "Backend OnHold Candidate",
                "OnHold",
                [
                    CreateInterview(backendOnHoldCandidateId, "Completed", "Proceed", 4, 4, 4),
                    CreateInterview(backendOnHoldCandidateId, "Completed", "Hire", 4, 5, 4)
                ],
                skills: ["Java", "Spring Boot", "Kafka"],
                matchedSkills: [],
                missingSkills: ["React", "Angular"],
                designation: "Senior Java Developer",
                jobTitle: "Backend Java Engineer"),
            CreateCandidate(
                reactCandidateId,
                "React Skill Match Candidate",
                "Rejected",
                [
                    CreateInterview(reactCandidateId, "Completed", "Proceed", 4, 4, 4)
                ]));
        var agent = CreateAgent(new CapturingVectorStore(new Dictionary<Guid, decimal>
        {
            [backendOnHoldCandidateId] = 0.6m,
            [reactCandidateId] = 0.6m
        }));

        var result = await agent.RankAsync(TenantId, context, CancellationToken.None);

        Assert.Equal(reactCandidateId, result.Matches[0].CandidateId);
        Assert.True(result.Matches[0].Score > result.Matches[1].Score);
    }

    [Fact]
    public async Task RankAsync_CapsZeroSkillCoverageCandidatesAtLowFitEvenWithWarmHistoryAndHighVectorScore()
    {
        var backendOnHoldCandidateId = Guid.Parse("33333333-3333-3333-3333-33333333333b");
        var reactCandidateId = Guid.Parse("33333333-3333-3333-3333-33333333333c");
        var context = CreatePriorityContext(
            CreateCandidate(
                backendOnHoldCandidateId,
                "Farah Backend Candidate",
                "OnHold",
                [
                    CreateInterview(backendOnHoldCandidateId, "Completed", "Proceed", 5, 5, 5),
                    CreateInterview(backendOnHoldCandidateId, "Completed", "Hire", 5, 5, 5)
                ],
                skills: ["Java", "Spring Boot", "Kafka", "Microservices"],
                matchedSkills: [],
                missingSkills: ["React", "Angular"],
                designation: "Senior Java Developer",
                jobTitle: "Java Platform Engineer",
                finalReason: "Cleared all interviews and kept warm, but backend-only profile with no React evidence."),
            CreateCandidate(
                reactCandidateId,
                "React Skill Match Candidate",
                "Rejected",
                [
                    CreateInterview(reactCandidateId, "Completed", "Proceed", 4, 4, 4)
                ],
                skills: ["React"],
                matchedSkills: ["React"],
                missingSkills: ["Angular"]));
        var agent = CreateAgent(new CapturingVectorStore(new Dictionary<Guid, decimal>
        {
            [backendOnHoldCandidateId] = 0.95m,
            [reactCandidateId] = 0.45m
        }));

        var result = await agent.RankAsync(TenantId, context, CancellationToken.None);
        var backendMatch = Assert.Single(result.Matches.Where(match => match.CandidateId == backendOnHoldCandidateId));

        Assert.Equal("Low", backendMatch.Confidence);
        Assert.True(backendMatch.Score <= 39m);
        Assert.Equal(reactCandidateId, result.Matches[0].CandidateId);
    }

    [Fact]
    public async Task RankAsync_ExcludesCandidatesWhoAlreadyJoinedOrWereHired()
    {
        var joinedCandidateId = Guid.Parse("33333333-3333-3333-3333-333333333339");
        var activeCandidateId = Guid.Parse("33333333-3333-3333-3333-33333333333a");
        var context = CreatePriorityContext(
            CreateCandidate(
                joinedCandidateId,
                "Joined Candidate",
                "Joined",
                [
                    CreateInterview(joinedCandidateId, "Completed", "Proceed", 4, 4, 4)
                ]),
            CreateCandidate(
                activeCandidateId,
                "Active Warm Candidate",
                "Rejected",
                [
                    CreateInterview(activeCandidateId, "Completed", "Proceed", 4, 4, 4)
                ]));
        var agent = CreateAgent(new CapturingVectorStore(new Dictionary<Guid, decimal>
        {
            [joinedCandidateId] = 0.9m,
            [activeCandidateId] = 0.6m
        }));

        var result = await agent.RankAsync(TenantId, context, CancellationToken.None);

        Assert.DoesNotContain(result.Matches, match => match.CandidateId == joinedCandidateId);
        Assert.Contains(result.Matches, match => match.CandidateId == activeCandidateId);
    }

    private static OperationsTalentRediscoveryContext CreateContext()
    {
        var jobRequest = new OperationsJobRequest(
            JobRequestId,
            "TP-REQ-200",
            "Senior React Developer",
            "Relia",
            "Build React customer portal features with Angular migration support and Azure deployment exposure.",
            "Engineering",
            ["React", "Angular", "Azure"],
            "4-7 years",
            "Lahore",
            1,
            0,
            "High",
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            "Recruiter Sourcing",
            null,
            "Sara Malik",
            "NotPublished",
            DateTimeOffset.UtcNow);

        var strongApplicationId = Guid.Parse("66666666-6666-6666-6666-666666666601");
        var secondaryApplicationId = Guid.Parse("66666666-6666-6666-6666-666666666602");

        var candidates = new[]
        {
            new OperationsRediscoveryCandidate(
                StrongCandidateId,
                "Nida Farooq",
                "nida.farooq@example.com",
                "Active",
                "Senior React Developer",
                "Product Studio",
                6.5m,
                15,
                ["React", "Angular", "Azure", "SQL Server"],
                ["React", "Angular", "Azure"],
                [],
                [
                    new OperationsCandidateApplicationEvidence(
                        strongApplicationId,
                        Guid.Parse("77777777-7777-7777-7777-777777777701"),
                        "TP-HIST-010",
                        "React Portal Engineer",
                        "Relia",
                        "Engineering",
                        "Lahore",
                        "Rejected",
                        "Referral",
                        DateTimeOffset.UtcNow.AddMonths(-5),
                        DateTimeOffset.UtcNow.AddMonths(-4),
                        "Client selected a local full-stack profile; interviewer feedback stayed positive.")
                ],
                [
                    new OperationsCandidateInterviewEvidence(
                        Guid.Parse("88888888-8888-8888-8888-888888888801"),
                        strongApplicationId,
                        "Technical Interview",
                        "Completed",
                        "Proceed",
                        4,
                        4,
                        5,
                        "Strong React and portal delivery experience.",
                        DateTimeOffset.UtcNow.AddMonths(-4))
                ]),
            new OperationsRediscoveryCandidate(
                SecondaryCandidateId,
                "Omar Sheikh",
                "omar.sheikh@example.com",
                "Active",
                "Frontend Engineer",
                "Consulting Partner",
                4.5m,
                30,
                ["Angular", "React"],
                ["React", "Angular"],
                ["Azure"],
                [
                    new OperationsCandidateApplicationEvidence(
                        secondaryApplicationId,
                        Guid.Parse("77777777-7777-7777-7777-777777777702"),
                        "TP-HIST-011",
                        "Angular Product Engineer",
                        "Enterprise Client",
                        "Engineering",
                        "Remote",
                        "OfferDeclined",
                        "LinkedIn",
                        DateTimeOffset.UtcNow.AddMonths(-3),
                        DateTimeOffset.UtcNow.AddMonths(-2),
                        "Offer declined due to timing.")
                ],
                [])
        };

        return new OperationsTalentRediscoveryContext(jobRequest, null, "JobRequest", jobRequest.Skills, 4, 7, candidates);
    }

    private static TalentRediscoveryAgent CreateAgent(CapturingVectorStore vectorStore)
    {
        return new TalentRediscoveryAgent(
            new CapturingModelProvider("[]"),
            new StaticEmbeddingProvider(),
            vectorStore,
            new StaticRuntimeSettingsResolver(),
            new CapturingRunLogger());
    }

    private static OperationsTalentRediscoveryContext CreatePriorityContext(
        params OperationsRediscoveryCandidate[] candidates)
    {
        var jobRequest = new OperationsJobRequest(
            JobRequestId,
            "TP-REQ-201",
            "Senior React Developer",
            "Relia",
            "Build React customer portal features.",
            "Engineering",
            ["React", "Angular"],
            "4-7 years",
            "Lahore",
            1,
            0,
            "High",
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            "Recruiter Sourcing",
            null,
            "Sara Malik",
            "NotPublished",
            DateTimeOffset.UtcNow);

        return new OperationsTalentRediscoveryContext(jobRequest, null, "JobRequest", jobRequest.Skills, 4, 7, candidates);
    }

    private static OperationsRediscoveryCandidate CreateCandidate(
        Guid candidateId,
        string name,
        string applicationStatus,
        IReadOnlyList<OperationsCandidateInterviewEvidence> interviews,
        IReadOnlyList<string>? skills = null,
        IReadOnlyList<string>? matchedSkills = null,
        IReadOnlyList<string>? missingSkills = null,
        string designation = "Senior React Developer",
        string jobTitle = "React Portal Engineer",
        string? finalReason = null)
    {
        var applicationId = Guid.NewGuid();
        return new OperationsRediscoveryCandidate(
            candidateId,
            name,
            $"{name.Replace(" ", ".", StringComparison.OrdinalIgnoreCase).ToLowerInvariant()}@example.com",
            "Active",
            designation,
            "Product Studio",
            5.5m,
            15,
            skills ?? ["React", "Angular"],
            matchedSkills ?? ["React", "Angular"],
            missingSkills ?? [],
            [
                new OperationsCandidateApplicationEvidence(
                    applicationId,
                    Guid.NewGuid(),
                    "TP-HIST-020",
                    jobTitle,
                    "Relia",
                    "Engineering",
                    "Lahore",
                    applicationStatus,
                    "Referral",
                    DateTimeOffset.UtcNow.AddMonths(-3),
                    applicationStatus == "OnHold" ? null : DateTimeOffset.UtcNow.AddMonths(-2),
                    finalReason ?? (applicationStatus == "OnHold"
                        ? "Cleared interviews and kept warm for the next matching role."
                        : "Client selected another profile; feedback stayed positive."))
            ],
            interviews.Select(interview => interview with { JobApplicationId = applicationId }).ToArray());
    }

    private static OperationsCandidateInterviewEvidence CreateInterview(
        Guid candidateId,
        string status,
        string recommendation,
        int technicalScore,
        int communicationScore,
        int cultureScore)
    {
        return new OperationsCandidateInterviewEvidence(
            Guid.NewGuid(),
            candidateId,
            "Technical Interview",
            status,
            recommendation,
            technicalScore,
            communicationScore,
            cultureScore,
            "Historical feedback for a similar React role.",
            DateTimeOffset.UtcNow.AddMonths(-2));
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
            var value = text.Contains("Nida", StringComparison.OrdinalIgnoreCase) ? 0.9f : 0.2f;
            return Task.FromResult(Enumerable.Repeat(value, 768).ToArray());
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
            return Task.CompletedTask;
        }
    }
}
