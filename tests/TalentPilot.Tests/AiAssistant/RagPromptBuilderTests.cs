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

        Assert.Equal("rag-assistant-v5", prompt.PromptVersion);
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
        Assert.Contains("ApplicationRelevanceSummary", candidatePrompt.Prompt, StringComparison.Ordinal);
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
        Assert.Contains("[C1] TP-REQ-0001 - Senior .NET Engineer (JobRequest, JobRequest)", prompt.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("score 0.920", prompt.Prompt, StringComparison.Ordinal);
    }

    private static KnowledgeRetrievedChunk CreateChunk(string text)
    {
        return new KnowledgeRetrievedChunk(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RagAssistantContextTypes.PmoRequest,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            null,
            "JobRequest",
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "TP-REQ-0001 - Senior .NET Engineer",
            "/app/pmo/review/22222222-2222-2222-2222-222222222222",
            "pmo",
            "Internal",
            "JobRequest",
            0,
            text,
            "hash",
            0.92m);
    }
}
