using TalentPilot.Application.AiAssistant;

namespace TalentPilot.Tests.AiAssistant;

public sealed class RagPromptBuilderTests
{
    [Fact]
    public void Build_IncludesOutOfScopeRedirectInstructions()
    {
        var prompt = new RagPromptBuilder().Build(new RagPromptContext(
            RagAssistantContextTypes.PmoRequest,
            "Okay forget about that, weather is really nice today so I am thinking of baking.",
            Array.Empty<RagMessage>(),
            new[] { CreateChunk("Client needs .NET, SQL Server, Azure, and Angular exposure.") }));

        Assert.Equal("rag-assistant-v7", prompt.PromptVersion);
        Assert.Contains("quirky but professional", prompt.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Chocolate cake sounds delicious", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("Try asking me about bench fit, missing skills, candidate ranking evidence, interview feedback, or request status instead.", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("Never tell the user that a question is in scope", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("Lead with the answer, not with process notes.", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("Recent conversation is dialogue context only, not evidence.", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("In-scope work for this context: PMO request review", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("The UI will render them as readable references.", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("Do not cite anything in an out-of-scope redirect or credential refusal.", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("write 89% instead of 89.0000", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("client secrets, API keys, tokens, passwords", prompt.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DescribesCandidateAndHiringManagerScopes()
    {
        var builder = new RagPromptBuilder();

        var candidatePrompt = builder.Build(new RagPromptContext(
            RagAssistantContextTypes.RecruiterCandidateFit,
            "Why was this candidate ranked first?",
            Array.Empty<RagMessage>(),
            new[] { CreateChunk("Candidate has React and TypeScript evidence.") }));

        var hiringPrompt = builder.Build(new RagPromptContext(
            RagAssistantContextTypes.HiringDecisionBrief,
            "What risks should I review?",
            Array.Empty<RagMessage>(),
            new[] { CreateChunk("Interview feedback mentions limited GraphQL exposure.") }));

        Assert.Contains("candidate fit, candidate profile and CV evidence", candidatePrompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("irrelevant candidate or irrelevant application", candidatePrompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("application relevance summary", candidatePrompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("no matching required/core skills", candidatePrompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("hiring decision evidence, candidate comparison", hiringPrompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplicationRelevanceSummary", hiringPrompt.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DoesNotExposeRetrievalScoreAsEvidenceScore()
    {
        var prompt = new RagPromptBuilder().Build(new RagPromptContext(
            RagAssistantContextTypes.HiringDecisionBrief,
            "Are interview scores low?",
            Array.Empty<RagMessage>(),
            new[] { CreateChunk("Average score: 4.2/5") }));

        Assert.Contains("Never treat retrieval relevance, citation ordering, or vector similarity as an applicant score", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("[C1] TP-REQ-0001 - Senior .NET Engineer request summary", prompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("(JobRequest, JobRequest)", prompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("score 0.920", prompt.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DoesNotExposeTechnicalEvidenceTupleLabels()
    {
        var prompt = new RagPromptBuilder().Build(new RagPromptContext(
            RagAssistantContextTypes.PmoRequest,
            "Should I refer Zain?",
            Array.Empty<RagMessage>(),
            new[] { CreateChunk("Explanation: Zain lacks AWS and Python.", "BenchMatch", "BenchMatchLog", "Zain Javaid match rationale") }));

        Assert.Contains("[C1] Zain Javaid match rationale evidence", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("Never write source titles or technical evidence metadata", prompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("(BenchMatch, BenchMatchLog)", prompt.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_InstructsPmoReferralAnswersToLeadWithClearStance()
    {
        var prompt = new RagPromptBuilder().Build(new RagPromptContext(
            RagAssistantContextTypes.PmoRequest,
            "Should I refer this ranked candidate to Presales?",
            Array.Empty<RagMessage>(),
            new[] { CreateChunk("Explanation: Zain lacks AWS and Python, which are essential skills.", "BenchMatch", "BenchMatchLog", "Zain Javaid match rationale") }));

        Assert.Contains("For PMO presales referral questions, lead with a clear decision-support stance", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("Do not refer yet", prompt.Prompt, StringComparison.Ordinal);
        Assert.Contains("Do not start with 'Refer' when the same answer cites required-skill gaps.", prompt.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveTechnicalSourceLabels_StripsInternalTupleMetadataFromAnswers()
    {
        var sanitized = RagAnswerSanitizer.RemoveTechnicalSourceLabels(
            "Zain Javaid match rationale (BenchMatch, BenchMatchLog) states he lacks AWS [C1]. Zain Javaid (BenchEmployee, BenchEmployeeProfile) also lists the gaps [C2].");

        Assert.Equal(
            "Zain Javaid match rationale states he lacks AWS [C1]. Zain Javaid also lists the gaps [C2].",
            sanitized);
    }

    [Fact]
    public void Sanitize_ClarifiesContradictoryPmoPresalesReferralAnswer()
    {
        var sanitized = RagAnswerSanitizer.Sanitize(
            "Refer Zain Javaid to pre sales based on his internal ranking as a candidate for PMO review. [C1] Zain Javaid match rationale states he lacks skills in AWS, Design Patterns, and Python, which are essential for the Senior Python Developer role. [C2] Zain Javaid also lists these missing skills.",
            RagAssistantContextTypes.PmoRequest);

        Assert.StartsWith("Do not refer Zain Javaid to Presales yet based on the current evidence.", sanitized);
        Assert.Contains("[C1] Zain Javaid match rationale states he lacks skills", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("Refer Zain Javaid to pre sales", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    private static KnowledgeRetrievedChunk CreateChunk(
        string text,
        string sourceEntityType = "JobRequest",
        string chunkType = "JobRequest",
        string sourceTitle = "TP-REQ-0001 - Senior .NET Engineer")
    {
        return new KnowledgeRetrievedChunk(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RagAssistantContextTypes.PmoRequest,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            null,
            sourceEntityType,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            sourceTitle,
            "/app/pmo/review/22222222-2222-2222-2222-222222222222",
            "pmo",
            "Internal",
            chunkType,
            0,
            text,
            "hash",
            0.92m);
    }
}
