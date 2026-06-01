namespace TalentPilot.Application.Operations;

public static class InterviewFeedbackPolicy
{
    private static readonly IReadOnlyDictionary<string, string> RecommendationMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["proceed"] = "Proceed",
            ["pass"] = "Proceed",
            ["yes"] = "Proceed",
            ["hold"] = "Hold",
            ["maybe"] = "Hold",
            ["reject"] = "Reject",
            ["nohire"] = "Reject",
            ["no hire"] = "Reject",
            ["fail"] = "Reject"
        };

    public static InterviewFeedbackValidation Validate(SubmitInterviewFeedbackInput input)
    {
        if (!IsScoreValid(input.TechnicalScore) ||
            !IsScoreValid(input.CommunicationScore) ||
            !IsScoreValid(input.CultureScore))
        {
            return InterviewFeedbackValidation.Invalid(
                "interview_feedback.score_invalid",
                "Scores must be between 1 and 5.");
        }

        if (string.IsNullOrWhiteSpace(input.Recommendation) ||
            !RecommendationMap.TryGetValue(input.Recommendation.Trim(), out var recommendation))
        {
            return InterviewFeedbackValidation.Invalid(
                "interview_feedback.recommendation_invalid",
                "Recommendation must be Proceed, Hold, or Reject.");
        }

        var feedbackText = input.FeedbackText?.Trim();
        if (string.IsNullOrWhiteSpace(feedbackText))
        {
            return InterviewFeedbackValidation.Invalid(
                "interview_feedback.feedback_required",
                "Feedback comments are required before completing an interview.");
        }

        return InterviewFeedbackValidation.Valid(input with
        {
            Recommendation = recommendation,
            FeedbackText = feedbackText
        });
    }

    private static bool IsScoreValid(int score)
    {
        return score is >= 1 and <= 5;
    }
}

public sealed record InterviewFeedbackValidation(
    bool Succeeded,
    SubmitInterviewFeedbackInput? NormalizedInput,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static InterviewFeedbackValidation Valid(SubmitInterviewFeedbackInput input)
    {
        return new InterviewFeedbackValidation(true, input, null, null);
    }

    public static InterviewFeedbackValidation Invalid(string code, string message)
    {
        return new InterviewFeedbackValidation(false, null, code, message);
    }
}
