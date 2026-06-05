using System.Text;

namespace TalentPilot.Application.AiAssistant;

public sealed class RagPromptBuilder : IRagPromptBuilder
{
    public const string CurrentPromptVersion = "rag-assistant-v5";

    public RagPrompt Build(RagPromptContext context)
    {
        var citations = context.Evidence
            .Select((chunk, index) => new RagCitationDraft(
                chunk.KnowledgeChunkId,
                $"C{index + 1}",
                chunk.SourceTitle,
                chunk.SourceEntityType,
                chunk.SourceEntityId,
                chunk.SourceRoute,
                chunk.Score,
                Excerpt(chunk.Text)))
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("You are Talent Pilot's internal conversational RAG assistant.");
        builder.AppendLine("Silently decide whether the user's latest question is in scope for the current Talent Pilot assistant context.");
        builder.AppendLine("Never tell the user that a question is in scope, out of scope, or part of a specific assistant context. Do not expose your routing or classification reasoning.");
        builder.AppendLine("Write naturally for a Talent Pilot user. Lead with the answer, not with process notes.");
        builder.AppendLine($"In-scope work for this context: {DescribeScope(context.ContextType)}");
        builder.AppendLine("If the latest question is clearly unrelated to Talent Pilot, recruiting, hiring workflows, candidates, job requests, bench matching, interviews, decisions, configuration, or the provided context, do not answer the unrelated topic.");
        builder.AppendLine("For clearly unrelated questions, reply in one short, quirky but professional paragraph using this pattern: \"Chocolate cake sounds delicious, but it's a bit outside my area of expertise. I'm designed to help with Talent Pilot hiring and workflow evidence. Try asking me about bench fit, missing skills, candidate ranking evidence, interview feedback, or request status instead.\" Adapt the opening phrase to the user's topic, but keep the redirect to Talent Pilot tasks.");
        builder.AppendLine("If the user asks you to find, reveal, recover, display, or share credentials, client secrets, API keys, tokens, passwords, private keys, or connection strings, refuse briefly and direct them to authorized admin rotation or the approved secrets store. Do not cite evidence for credential refusals.");
        builder.AppendLine("For in-scope questions, use only the evidence chunks provided below. If the evidence does not support the answer, say that the available evidence is insufficient.");
        builder.AppendLine("Recent conversation is dialogue context only, not evidence. If a prior assistant answer conflicts with the evidence chunks, ignore the prior answer and correct it from the evidence.");
        if (RagAssistantContextTypes.Normalize(context.ContextType) == RagAssistantContextTypes.RecruiterCandidateFit)
        {
            builder.AppendLine("For recruiter candidate-fit questions, treat an irrelevant candidate or irrelevant application as one where the evidence shows no matching required/core skills, or the candidate profile specialization and day-to-day work are dissimilar to the role requirement. Related broad industry experience is not enough when the core role skills and responsibilities are missing.");
            builder.AppendLine("When evidence includes an ApplicationRelevanceSummary chunk, use its counts and categories for questions about irrelevant applications, irrelevant candidates, poor matches, no matching skills, profile mismatch, dissimilar profiles, or applications not aligned with the role. Do not count borderline or lower-ranked candidates as irrelevant unless evidence labels them irrelevant/poor match or shows no matching core skills or profile mismatch.");
        }
        builder.AppendLine("Cite evidence with the provided citation labels, for example [C1] or [C2], only as source markers. Do not call them chunks or explain the citation mechanics. The UI will render them as readable references.");
        builder.AppendLine("Do not cite anything in an out-of-scope redirect or credential refusal.");
        builder.AppendLine("Format numbers for humans. For example, write 89% instead of 89.0000 when the value is a percentage or whole-number score.");
        builder.AppendLine("Never treat retrieval relevance, citation ordering, or vector similarity as an applicant score, interview score, or hiring threshold.");
        builder.AppendLine("Do not approve, reject, hire, allocate, move workflow stages, schedule meetings, generate offers, or contact any person. Explain and summarize only.");
        builder.AppendLine("Do not expose sensitive data beyond the provided evidence, and do not infer protected attributes.");
        builder.AppendLine();
        builder.AppendLine($"Assistant context: {context.ContextType}");

        var history = context.ConversationHistory
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .TakeLast(6)
            .ToArray();
        if (history.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Recent conversation:");
            foreach (var message in history)
            {
                builder.AppendLine($"{message.Role}: {TrimForPrompt(message.Content, 900)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Evidence:");
        for (var index = 0; index < context.Evidence.Count; index++)
        {
            var chunk = context.Evidence[index];
            var citation = citations[index];
            builder.AppendLine($"[{citation.Label}] {chunk.SourceTitle} ({chunk.SourceEntityType}, {chunk.ChunkType})");
            builder.AppendLine(TrimForPrompt(chunk.Text, 1800));
            builder.AppendLine();
        }

        builder.AppendLine("User question:");
        builder.AppendLine(context.UserQuestion.Trim());
        builder.AppendLine();
        builder.AppendLine("Answer concisely, with citations beside the claims they support.");

        return new RagPrompt(CurrentPromptVersion, builder.ToString(), citations);
    }

    private static string Excerpt(string text)
    {
        return TrimForPrompt(text.ReplaceLineEndings(" "), 280);
    }

    private static string DescribeScope(string contextType)
    {
        return RagAssistantContextTypes.Normalize(contextType) switch
        {
            RagAssistantContextTypes.PmoRequest =>
                "PMO request review, request status, bench fit, internal employee matches, missing skills, referrals, presales handoff, recruiter handoff, and workflow evidence.",
            RagAssistantContextTypes.RecruiterCandidateFit =>
                "candidate fit, candidate profile and CV evidence, job post requirements, application status, ranking reasons, skill gaps, recruiter notes, and interview evidence.",
            RagAssistantContextTypes.HiringDecisionBrief =>
                "hiring decision evidence, candidate comparison, interview feedback, decision brief risks, strengths, ranking evidence, and hiring manager review context.",
            _ =>
                "Talent Pilot hiring operations, workflow status, candidates, job requests, evidence summaries, rankings, and configuration visible in the provided context."
        };
    }

    private static string TrimForPrompt(string text, int maxLength)
    {
        var normalized = string.Join(' ', text.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength]}...";
    }
}
