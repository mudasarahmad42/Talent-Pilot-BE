namespace TalentPilot.Tests.Domain;

public sealed class SeedDataConsistencyTests
{
    [Fact]
    public void AiAnalyticsSeed_OfferDeclinedRediscoveryCandidateClearedAllInterviews()
    {
        var seed = ReadBackendFile("scripts", "seed", "004_seed_ai_analytics_demo_data.sql");

        Assert.Contains("@SanaHistApplicationId", seed, StringComparison.Ordinal);
        Assert.Contains("N'OfferDeclined'", seed, StringComparison.Ordinal);
        Assert.Contains("N'Candidate cleared all interviews, received an offer, and then accepted a counter offer.'", seed, StringComparison.Ordinal);
        Assert.Contains("('26200000-0000-0000-0000-000000000008', '26100000-0000-0000-0000-000000000008', @InterviewerUserId, 4, 4, 4, N'Proceed'", seed, StringComparison.Ordinal);
        Assert.Contains("('26200000-0000-0000-0000-000000000009', '26100000-0000-0000-0000-000000000009', @HodUserId, 4, 4, 4, N'Proceed'", seed, StringComparison.Ordinal);
        Assert.Contains("\"interviewPassSummary\":\"3/3 passed\"", seed, StringComparison.Ordinal);
        Assert.DoesNotContain("\"interviewPassSummary\":\"1/3 passed\"", seed, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_RepairsPersistedOfferDeclinedRediscoveryPayloads()
    {
        var migration = ReadBackendFile("scripts", "migrations", "025_fix_offer_declined_seed_interviews.sql");

        Assert.Contains("OfferDeclined should only appear after the candidate has cleared every configured interview.", migration, StringComparison.Ordinal);
        Assert.Contains("N'Proceed'", migration, StringComparison.Ordinal);
        Assert.Contains("N'\"interviewPassSummary\":\"1/3 passed\"'", migration, StringComparison.Ordinal);
        Assert.Contains("N'\"interviewPassSummary\":\"3/3 passed\"'", migration, StringComparison.Ordinal);
        Assert.Contains("N'\"interviewsPassed\":1'", migration, StringComparison.Ordinal);
        Assert.Contains("N'\"interviewsPassed\":3'", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_SeedsReactTalentRediscoveryCandidatePool()
    {
        var migration = ReadBackendFile("scripts", "migrations", "044_seed_react_talent_rediscovery_candidates.sql");

        var candidateRows = System.Text.RegularExpressions.Regex.Matches(
            migration,
            @"^\s*\(\d+, N'[^']+', N'[A-Z]{2}', N'[^']+@8pkk57\.onmicrosoft\.com'",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        Assert.Equal(45, candidateRows.Count);
        Assert.Contains("DECLARE @ExpectedCandidateCount INT = 45;", migration, StringComparison.Ordinal);
        Assert.Contains("THROW 51044", migration, StringComparison.Ordinal);
        Assert.Contains("ReactRediscoverySeed", migration, StringComparison.Ordinal);
        Assert.Contains("MERGE dbo.JobApplicationDocuments", migration, StringComparison.Ordinal);
        Assert.Contains("ExtractedTextHashSha256", migration, StringComparison.Ordinal);
        Assert.Contains("N'web performance optimization'", migration, StringComparison.Ordinal);
        Assert.Contains("N'rest api integration'", migration, StringComparison.Ordinal);
        Assert.Contains("N'tailwind css'", migration, StringComparison.Ordinal);
        Assert.Contains("N'ant design'", migration, StringComparison.Ordinal);
    }

    private static string ReadBackendFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "scripts")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var path = Path.Combine(new[] { directory!.FullName }.Concat(pathParts).ToArray());
        Assert.True(File.Exists(path), $"Expected file to exist: {path}");
        return File.ReadAllText(path);
    }
}
