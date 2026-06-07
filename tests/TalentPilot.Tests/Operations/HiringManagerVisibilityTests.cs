namespace TalentPilot.Tests.Operations;

public sealed class HiringManagerVisibilityTests
{
    [Fact]
    public void HiringManagerReviewQueries_KeepHiredCandidatesVisibleUntilJoined()
    {
        var repository = ReadBackendFile(
            "src",
            "TalentPilot.Infrastructure",
            "Persistence",
            "Repositories",
            "DapperOperationsRepository.cs");

        Assert.Contains(
            "N'HiringManagerReview', N'Offered', N'OnHold', N'Rejected', N'Hired', N'Joined'",
            repository,
            StringComparison.Ordinal);
        Assert.Contains(
            "IsDashboardStatus(row.Status, \"Hired\")",
            repository,
            StringComparison.Ordinal);
        Assert.Contains(
            "new HiringManagerDashboardStatusBreakdownItem(\"Pending joining\", rows.Count(row => IsDashboardStatus(row.Status, \"Hired\")))",
            repository,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HiringManagerReviewList_ExposesOfferLetterStateForMyWork()
    {
        var repository = ReadBackendFile(
            "src",
            "TalentPilot.Infrastructure",
            "Persistence",
            "Repositories",
            "DapperOperationsRepository.cs");

        Assert.Contains("WITH LatestOffer AS", repository, StringComparison.Ordinal);
        Assert.Contains("latestOffer.Status AS OfferLetterStatus", repository, StringComparison.Ordinal);
        Assert.Contains("meetingAgg.LatestMeetingAtUtc AS LatestMeetingAt", repository, StringComparison.Ordinal);
        Assert.Contains("row.OfferLetterStatus", repository, StringComparison.Ordinal);
        Assert.Contains("ToUtc(row.LatestMeetingAt)", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void HiringManagerReviewAccessQuery_UsesAuditLogOccurredTimestamp()
    {
        var repository = ReadBackendFile(
            "src",
            "TalentPilot.Infrastructure",
            "Persistence",
            "Repositories",
            "DapperOperationsRepository.cs");

        var closeAuditQueryStart = repository.IndexOf(
            "OUTER APPLY (\r\n                SELECT TOP (1) audit.EventSummary",
            StringComparison.Ordinal);
        Assert.True(closeAuditQueryStart >= 0, "Expected hiring review close audit query to exist.");

        var closeAuditQueryEnd = repository.IndexOf(") AS closeAudit", closeAuditQueryStart, StringComparison.Ordinal);
        Assert.True(closeAuditQueryEnd > closeAuditQueryStart, "Expected hiring review close audit query to terminate.");

        var closeAuditQuery = repository[closeAuditQueryStart..closeAuditQueryEnd];
        Assert.Contains("ORDER BY audit.OccurredAtUtc DESC", closeAuditQuery, StringComparison.Ordinal);
        Assert.DoesNotContain("ORDER BY audit.CreatedAtUtc DESC", closeAuditQuery, StringComparison.Ordinal);
    }

    private static string ReadBackendFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var path = Path.Combine(new[] { directory!.FullName }.Concat(pathParts).ToArray());
        Assert.True(File.Exists(path), $"Expected file to exist: {path}");
        return File.ReadAllText(path);
    }
}
