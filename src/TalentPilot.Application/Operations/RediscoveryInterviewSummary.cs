namespace TalentPilot.Application.Operations;

public sealed record RediscoveryInterviewPassSummary(int Passed, int Total, string DisplayText);

public static class RediscoveryInterviewSummary
{
    private static readonly HashSet<string> CountedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Scheduled",
        "Completed",
        "Skipped",
        "Cancelled"
    };

    public static RediscoveryInterviewPassSummary Build(IReadOnlyList<OperationsCandidateInterviewEvidence> interviews)
    {
        var countedInterviews = interviews
            .Where(interview => CountedStatuses.Contains(interview.Status))
            .ToArray();
        var passed = countedInterviews.Count(IsPassedInterview);
        var total = countedInterviews.Length;

        return new RediscoveryInterviewPassSummary(passed, total, $"{passed}/{total} passed");
    }

    public static bool IsPassedInterview(OperationsCandidateInterviewEvidence interview)
    {
        if (!string.Equals(interview.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(interview.Recommendation, "Proceed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var scores = new[]
            {
                interview.TechnicalScore,
                interview.CommunicationScore,
                interview.CultureScore
            }
            .Where(score => score.HasValue)
            .Select(score => score!.Value)
            .ToArray();

        return scores.Length > 0 && scores.Average() >= 3.5;
    }
}
