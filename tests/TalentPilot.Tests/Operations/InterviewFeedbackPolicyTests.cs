using TalentPilot.Application.Operations;

namespace TalentPilot.Tests.Operations;

public sealed class InterviewFeedbackPolicyTests
{
    [Fact]
    public void Validate_NormalizesRecommendationAndFeedback()
    {
        var input = new SubmitInterviewFeedbackInput(
            TechnicalScore: 4,
            CommunicationScore: 5,
            CultureScore: 3,
            Recommendation: "pass",
            FeedbackText: "  Strong technical fit.  ");

        var result = InterviewFeedbackPolicy.Validate(input);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.NormalizedInput);
        Assert.Equal("Proceed", result.NormalizedInput.Recommendation);
        Assert.Equal("Strong technical fit.", result.NormalizedInput.FeedbackText);
    }

    [Theory]
    [InlineData(0, 4, 4)]
    [InlineData(4, 6, 4)]
    [InlineData(4, 4, -1)]
    public void Validate_RejectsScoresOutsideOneToFive(
        int technicalScore,
        int communicationScore,
        int cultureScore)
    {
        var input = new SubmitInterviewFeedbackInput(
            technicalScore,
            communicationScore,
            cultureScore,
            "Proceed",
            "Candidate can move ahead.");

        var result = InterviewFeedbackPolicy.Validate(input);

        Assert.False(result.Succeeded);
        Assert.Equal("interview_feedback.score_invalid", result.ErrorCode);
    }

    [Fact]
    public void Validate_RejectsMissingFeedback()
    {
        var input = new SubmitInterviewFeedbackInput(4, 4, 4, "Proceed", " ");

        var result = InterviewFeedbackPolicy.Validate(input);

        Assert.False(result.Succeeded);
        Assert.Equal("interview_feedback.feedback_required", result.ErrorCode);
    }
}
