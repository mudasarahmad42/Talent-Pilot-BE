using TalentPilot.Application.Operations;

namespace TalentPilot.Tests.Operations;

public sealed class RediscoveryInterviewSummaryTests
{
    [Fact]
    public void Build_CountsProceedRecommendationAsPassed()
    {
        var interview = CreateInterview("Completed", "Proceed", 2, 2, 2);

        var summary = RediscoveryInterviewSummary.Build([interview]);

        Assert.Equal(1, summary.Passed);
        Assert.Equal(1, summary.Total);
        Assert.Equal("1/1 passed", summary.DisplayText);
    }

    [Fact]
    public void Build_CountsAverageScoreAtThresholdAsPassed()
    {
        var interview = CreateInterview("Completed", "NoHire", 4, 4, 3);

        var summary = RediscoveryInterviewSummary.Build([interview]);

        Assert.Equal(1, summary.Passed);
        Assert.Equal(1, summary.Total);
    }

    [Fact]
    public void Build_CountsScheduledSkippedAndCancelledInTotalButNotPassed()
    {
        var interviews = new[]
        {
            CreateInterview("Scheduled", null, null, null, null),
            CreateInterview("Skipped", null, null, null, null),
            CreateInterview("Cancelled", null, null, null, null),
            CreateInterview("NoShow", null, null, null, null)
        };

        var summary = RediscoveryInterviewSummary.Build(interviews);

        Assert.Equal(0, summary.Passed);
        Assert.Equal(3, summary.Total);
        Assert.Equal("0/3 passed", summary.DisplayText);
    }

    [Fact]
    public void Build_UsesConfiguredRoundCountWhenNoInterviewsAreScheduled()
    {
        var summary = RediscoveryInterviewSummary.Build([], configuredTotal: 3);

        Assert.Equal(0, summary.Passed);
        Assert.Equal(3, summary.Total);
        Assert.Equal("0/3 passed", summary.DisplayText);
    }

    private static OperationsCandidateInterviewEvidence CreateInterview(
        string status,
        string? recommendation,
        int? technicalScore,
        int? communicationScore,
        int? cultureScore)
    {
        return new OperationsCandidateInterviewEvidence(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Technical Interview",
            status,
            recommendation,
            technicalScore,
            communicationScore,
            cultureScore,
            "Feedback",
            DateTimeOffset.UtcNow);
    }
}
