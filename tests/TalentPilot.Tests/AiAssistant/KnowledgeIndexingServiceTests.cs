using System.Reflection;
using TalentPilot.Application.AiAssistant;
using TalentPilot.Application.Operations;

namespace TalentPilot.Tests.AiAssistant;

public sealed class KnowledgeIndexingServiceTests
{
    [Fact]
    public void BuildRecruiterCandidateFitChunks_AddsApplicationRelevanceSummary()
    {
        var requestId = Guid.Parse("24000000-0000-0000-0000-000000000001");
        var amaraApplicationId = Guid.Parse("24000000-0000-0000-0000-000000000101");
        var hiraApplicationId = Guid.Parse("24000000-0000-0000-0000-000000000102");

        var sourcing = new OperationsRecruiterSourcing(
            CreateJobRequest(requestId),
            null,
            CreateJobPost(requestId),
            new[]
            {
                CreateApplication(amaraApplicationId, "Amara Haq", "Java Backend Engineer"),
                CreateApplication(hiraApplicationId, "Hira Saleem", "Data Engineer")
            },
            Array.Empty<OperationsManualCandidateSearchItem>(),
            Array.Empty<OperationsTalentRediscoveryMatch>(),
            new[]
            {
                CreateRanking(
                    amaraApplicationId,
                    "Amara Haq",
                    "Java Backend Engineer",
                    1,
                    89m,
                    Array.Empty<string>(),
                    Array.Empty<string>()),
                CreateRanking(
                    hiraApplicationId,
                    "Hira Saleem",
                    "Data Engineer",
                    2,
                    51m,
                    new[] { "SQL" },
                    new[] { "Java", "Spring Boot", "Microservices", "Kafka" })
            },
            Array.Empty<OperationsInterviewTemplateOption>(),
            Array.Empty<OperationsInterviewerOption>(),
            Array.Empty<OperationsLookupOption>(),
            Array.Empty<OperationsLookupOption>(),
            null);

        var chunks = InvokeBuildRecruiterCandidateFitChunks(sourcing);
        var summary = Assert.Single(chunks.Where(chunk => chunk.ChunkType == "ApplicationRelevanceSummary"));

        Assert.Contains("Irrelevant applications: 1", summary.Text, StringComparison.Ordinal);
        Assert.Contains("Relevant applications: 1", summary.Text, StringComparison.Ordinal);
        Assert.Contains("Candidate: Amara Haq", summary.Text, StringComparison.Ordinal);
        Assert.Contains("Irrelevant: No", summary.Text, StringComparison.Ordinal);
        Assert.Contains("Candidate: Hira Saleem", summary.Text, StringComparison.Ordinal);
        Assert.Contains("Irrelevant: Yes", summary.Text, StringComparison.Ordinal);
        Assert.Contains("profile specialization does not align with the role", summary.Text, StringComparison.Ordinal);
        Assert.Contains("no matching skills, profile mismatch, dissimilar profile", summary.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHiringDecisionBriefChunks_LabelsInterviewScoresOutOfFive()
    {
        var applicationId = Guid.Parse("24000000-0000-0000-0000-000000000101");
        var detail = CreateHiringReviewDetail(applicationId);

        var chunks = InvokeBuildHiringDecisionBriefChunks(detail, applicationId);
        var interviewChunk = Assert.Single(chunks.Where(chunk => chunk.ChunkType == "InterviewFeedback"));

        Assert.Contains("Technical score: 4/5", interviewChunk.Text, StringComparison.Ordinal);
        Assert.Contains("Communication score: 5/5", interviewChunk.Text, StringComparison.Ordinal);
        Assert.Contains("Culture score: 4/5", interviewChunk.Text, StringComparison.Ordinal);
        Assert.Contains("Average score: 4.2/5", interviewChunk.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPmoRequestChunks_SanitizesStoredBenchMatchRationale()
    {
        var review = CreatePmoReviewWithStaleBenchMatchRationale();

        var chunks = InvokeBuildPmoRequestChunks(review);
        var benchMatchChunk = Assert.Single(chunks.Where(chunk => chunk.ChunkType == "BenchMatchLog"));

        Assert.Contains("Zain Javaid's profile is primarily Java", benchMatchChunk.Text, StringComparison.Ordinal);
        Assert.Contains("6.8 years overall", benchMatchChunk.Text, StringComparison.Ordinal);
        Assert.Contains("this request is centered on Python, AWS, SQL, and Design Patterns", benchMatchChunk.Text, StringComparison.Ordinal);
        Assert.Contains("current tenant evidence only supports SQL", benchMatchChunk.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("less than the required 3+ years", benchMatchChunk.Text, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<KnowledgeChunkDraft> InvokeBuildPmoRequestChunks(OperationsPmoReview review)
    {
        var method = typeof(KnowledgeIndexingService).GetMethod(
            "BuildPmoRequestChunks",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var chunks = method.Invoke(null, new object?[] { review });
        return Assert.IsAssignableFrom<IReadOnlyList<KnowledgeChunkDraft>>(chunks);
    }

    private static IReadOnlyList<KnowledgeChunkDraft> InvokeBuildRecruiterCandidateFitChunks(OperationsRecruiterSourcing sourcing)
    {
        var method = typeof(KnowledgeIndexingService).GetMethod(
            "BuildRecruiterCandidateFitChunks",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var chunks = method.Invoke(null, new object?[] { sourcing, null });
        return Assert.IsAssignableFrom<IReadOnlyList<KnowledgeChunkDraft>>(chunks);
    }

    private static IReadOnlyList<KnowledgeChunkDraft> InvokeBuildHiringDecisionBriefChunks(
        HiringReviewDetail detail,
        Guid jobApplicationId)
    {
        var method = typeof(KnowledgeIndexingService).GetMethod(
            "BuildHiringDecisionBriefChunks",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var chunks = method.Invoke(null, new object?[] { detail, jobApplicationId });
        return Assert.IsAssignableFrom<IReadOnlyList<KnowledgeChunkDraft>>(chunks);
    }

    private static OperationsPmoReview CreatePmoReviewWithStaleBenchMatchRationale()
    {
        var requestId = Guid.Parse("25000000-0000-0000-0000-000000000001");
        var employeeId = Guid.Parse("25000000-0000-0000-0000-000000000101");
        var jobRequest = new OperationsJobRequest(
            requestId,
            "TP-REQ-021",
            "Senior Python Developer",
            "Tesla",
            "EV manufacturing client context for a Lahore engineering office.",
            "Build Python backend services for engineering workflows.",
            "Engineering",
            new[] { "Python", "AWS", "SQL", "Design Patterns" },
            "3+ years",
            "Lahore",
            1,
            0,
            "High",
            Guid.Parse("25000000-0000-0000-0000-000000000201"),
            Guid.Parse("25000000-0000-0000-0000-000000000202"),
            "PMO Review",
            null,
            "PMO - Engineering",
            "NotPublished",
            DateTimeOffset.Parse("2026-06-01T10:00:00Z"));
        var employee = new OperationsBenchEmployee(
            employeeId,
            "Zain Javaid",
            "zain.javaid@example.test",
            "Senior Java Engineer",
            "Engineering",
            "Lahore",
            6.8m,
            DateOnly.Parse("2021-02-15"),
            "Available",
            "Benched",
            true,
            new[] { "Java", "SQL" },
            new[] { "SQL" },
            new[] { "Python", "AWS", "Design Patterns" },
            new[]
            {
                new OperationsEmployeeProjectEvidence(
                    "AZAQ Payment Modernization",
                    "AZAQ Saudia Arabia",
                    "Completed",
                    100,
                    DateOnly.Parse("2024-01-01"),
                    DateOnly.Parse("2024-12-31"))
            });
        var match = new OperationsBenchMatch(
            employeeId,
            1,
            59.4m,
            "Low",
            "Zain Javaid has 6.8 years of experience as a Senior Java Engineer, which is less than the required 3+ years for this role. Despite his experience, he lacks skills in AWS, Design Patterns, and Python, which are essential for this position.",
            Array.Empty<string>(),
            new[] { "Missing requested skill evidence: Python.", "Missing requested skill evidence: AWS.", "Missing requested skill evidence: Design Patterns." },
            employee.ProjectEvidence,
            "Skipped:LiveContextNotRequired",
            "Web search was skipped because this request did not ask for recent or live public context.",
            Array.Empty<OperationsBenchMatchWebSource>(),
            Guid.Parse("25000000-0000-0000-0000-000000000301"),
            DateTimeOffset.Parse("2026-06-04T13:04:56Z"));

        return new OperationsPmoReview(
            jobRequest,
            null,
            Array.Empty<OperationsEmployeeReferral>(),
            new[] { employee },
            new[] { match },
            Array.Empty<OperationsLookupOption>(),
            null,
            "Recruiting - Delivery");
    }

    private static OperationsJobRequest CreateJobRequest(Guid requestId)
    {
        return new OperationsJobRequest(
            requestId,
            "TP-DEMO-101",
            "Senior Java Backend Engineer",
            "AZAQ Saudia Arabia",
            "Payments modernization context for financial services integration work.",
            "Build Java backend services with Spring Boot, microservices, Kafka, and SQL.",
            "Engineering",
            new[] { "Java", "Spring Boot", "Microservices", "Kafka", "SQL" },
            "5-8 years",
            "Karachi",
            1,
            0,
            "High",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Recruiter Sourcing",
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            null,
            "Published",
            DateTimeOffset.Parse("2026-06-01T10:00:00Z"));
    }

    private static OperationsJobPost CreateJobPost(Guid requestId)
    {
        return new OperationsJobPost(
            Guid.Parse("24000000-0000-0000-0000-000000000201"),
            requestId,
            "Senior Java Backend Engineer",
            "Java backend role requiring Spring Boot, microservices, Kafka, and SQL.",
            "Engineering",
            "Karachi",
            5m,
            8m,
            1,
            "Published",
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Sourcing Manager",
            DateTimeOffset.Parse("2026-06-01T11:00:00Z"),
            null,
            DateTimeOffset.Parse("2026-06-01T10:30:00Z"),
            DateTimeOffset.Parse("2026-06-01T11:00:00Z"),
            new[]
            {
                new OperationsJobPostSkill(Guid.Parse("24000000-0000-0000-0000-000000000301"), "Java", "Backend"),
                new OperationsJobPostSkill(Guid.Parse("24000000-0000-0000-0000-000000000302"), "Spring Boot", "Backend"),
                new OperationsJobPostSkill(Guid.Parse("24000000-0000-0000-0000-000000000303"), "Kafka", "Backend")
            },
            Array.Empty<OperationsJobPostInterviewRound>());
    }

    private static OperationsRecruiterApplication CreateApplication(Guid applicationId, string candidateName, string designation)
    {
        return new OperationsRecruiterApplication(
            applicationId,
            Guid.NewGuid(),
            candidateName,
            $"{candidateName.Replace(" ", ".", StringComparison.OrdinalIgnoreCase).ToLowerInvariant()}@example.test",
            "Active",
            designation,
            "Product Studio",
            6.8m,
            15,
            "Applied",
            "Job Portal",
            "Talent Pilot portal",
            null,
            null,
            false,
            DateTimeOffset.Parse("2026-06-01T12:00:00Z"),
            0,
            2,
            "Not scheduled",
            Array.Empty<OperationsRecruiterApplicationDocument>(),
            Array.Empty<OperationsRecruiterApplicationInterview>());
    }

    private static OperationsApplicantRankingMatch CreateRanking(
        Guid applicationId,
        string candidateName,
        string designation,
        int rank,
        decimal score,
        IReadOnlyList<string> matchedSkills,
        IReadOnlyList<string> missingSkills)
    {
        return new OperationsApplicantRankingMatch(
            applicationId,
            Guid.NewGuid(),
            candidateName,
            $"{candidateName.Replace(" ", ".", StringComparison.OrdinalIgnoreCase).ToLowerInvariant()}@example.test",
            designation,
            6.8m,
            15,
            rank,
            score,
            "Medium",
            "Ranking generated from application and job requirement evidence.",
            Array.Empty<string>(),
            Array.Empty<string>(),
            matchedSkills,
            missingSkills,
            Array.Empty<string>(),
            Array.Empty<string>(),
            "Available",
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            DateTimeOffset.Parse("2026-06-04T13:04:56Z"));
    }

    private static HiringReviewDetail CreateHiringReviewDetail(Guid applicationId)
    {
        var candidateId = Guid.Parse("24000000-0000-0000-0000-000000000501");
        var jobRequestId = Guid.Parse("24000000-0000-0000-0000-000000000001");

        return new HiringReviewDetail(
            new HiringReviewCandidateSummary(
                candidateId,
                "Amara Haq",
                "amara@example.test",
                "Offered",
                "Java Backend Engineer",
                "Product Studio",
                6.8m,
                400000m,
                "PKR",
                15),
            new HiringReviewJobSummary(
                jobRequestId,
                Guid.Parse("24000000-0000-0000-0000-000000000201"),
                "TP-REQ-019",
                "Senior React Developer",
                "Client ABC",
                "Engineering",
                "Lahore",
                5m,
                8m,
                1,
                0,
                "Closed",
                "Offered",
                null,
                null,
                "Job Portal",
                "Talent Pilot portal",
                null,
                "Senior React Developer request.",
                "Senior React Developer post."),
            new[]
            {
                new HiringReviewInterviewDetail(
                    Guid.Parse("24000000-0000-0000-0000-000000000601"),
                    null,
                    "Technical Interview",
                    "Completed",
                    "Fatima Noor",
                    DateTimeOffset.Parse("2026-06-03T14:00:00Z"),
                    60,
                    "Proceed",
                    4,
                    5,
                    4,
                    4.2m,
                    "Strong implementation discussion.",
                    null,
                    DateTimeOffset.Parse("2026-06-03T15:00:00Z"))
            },
            "Decision brief text.",
            new HiringReviewDecisionBriefInsight(
                "hiring-manager-decision-brief",
                "Hiring Manager Decision Brief",
                "Interviewers recorded 3/3 positive recommendation(s) with 4.2/5 average scoring.",
                Array.Empty<HiringReviewDecisionMetric>(),
                Array.Empty<HiringReviewDecisionContextItem>(),
                Array.Empty<string>()),
            null,
            Array.Empty<OfferPresentationMeetingDetails>());
    }
}
