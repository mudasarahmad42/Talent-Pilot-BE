using System.Text.RegularExpressions;

namespace TalentPilot.Application.AiAssistant;

public static class RagAnswerSanitizer
{
    private static readonly Regex TechnicalSourceTuplePattern = new(
        @"\s*\((?:BenchMatch|BenchEmployee|JobRequest|CandidateApplication|ApplicantRanking|TalentRediscovery|EmployeeReferral|InterviewFeedback|ApplicationRelevanceSummary|HiringDecisionBrief|JobPost|CandidateProfile)[A-Za-z]*,\s*[A-Za-z]+(?:Log|Profile|Summary|Feedback|Request|Application|Match|Ranking|Brief)?\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LeadingPresalesReferralPattern = new(
        @"^\s*Refer\s+(?<name>[\p{L}\p{M}\s'.-]+?)\s+to\s+pre\s*sales\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Sanitize(string answer, string contextType)
    {
        var sanitized = RemoveTechnicalSourceLabels(answer);
        return RagAssistantContextTypes.Normalize(contextType) == RagAssistantContextTypes.PmoRequest
            ? ClarifyContradictoryPresalesReferralAnswer(sanitized)
            : sanitized;
    }

    public static string RemoveTechnicalSourceLabels(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return answer;
        }

        return TechnicalSourceTuplePattern
            .Replace(answer, string.Empty)
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static string ClarifyContradictoryPresalesReferralAnswer(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return answer;
        }

        var lower = answer.ToLowerInvariant();
        if (!ContainsAny(lower, "lacks", "missing", "not preferred") ||
            !ContainsAny(lower, "required", "essential", "skill"))
        {
            return answer;
        }

        var match = LeadingPresalesReferralPattern.Match(answer);
        if (!match.Success)
        {
            return answer;
        }

        var name = match.Groups["name"].Value.Trim();
        var remainingEvidence = RemoveFirstSentence(answer).Trim();
        if (remainingEvidence.Length == 0)
        {
            return $"Do not refer {name} to Presales yet based on the current evidence.";
        }

        return $"Do not refer {name} to Presales yet based on the current evidence. {remainingEvidence}";
    }

    private static string RemoveFirstSentence(string value)
    {
        var firstPeriod = value.IndexOf('.', StringComparison.Ordinal);
        return firstPeriod >= 0 && firstPeriod + 1 < value.Length
            ? value[(firstPeriod + 1)..]
            : string.Empty;
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}
