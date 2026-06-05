using TalentPilot.Application.AiAssistant;

namespace TalentPilot.Tests.AiAssistant;

public sealed class RagCitationUsageTests
{
    [Fact]
    public void FilterReferenced_ReturnsOnlyLabelsUsedInAnswer()
    {
        var citations = new[]
        {
            CreateCitation("C1"),
            CreateCitation("C2"),
            CreateCitation("C3")
        };

        var filtered = RagCitationUsage.FilterReferenced(
            citations,
            "Hamza is closer on .NET evidence [C1], but Angular is still a gap C3.");

        Assert.Collection(
            filtered,
            citation => Assert.Equal("C1", citation.Label),
            citation => Assert.Equal("C3", citation.Label));
    }

    [Fact]
    public void FilterReferenced_ReturnsEmptyForOutOfScopeAnswerWithoutCitationLabels()
    {
        var citations = new[] { CreateCitation("C1"), CreateCitation("C2") };

        var filtered = RagCitationUsage.FilterReferenced(
            citations,
            "Chocolate cake sounds delicious, but it is outside my Talent Pilot expertise.");

        Assert.Empty(filtered);
    }

    private static RagCitationDraft CreateCitation(string label)
    {
        return new RagCitationDraft(
            Guid.NewGuid(),
            label,
            "Source",
            "JobRequest",
            Guid.NewGuid(),
            "/app/pmo/review/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeee01",
            0.9m,
            "Excerpt");
    }
}
