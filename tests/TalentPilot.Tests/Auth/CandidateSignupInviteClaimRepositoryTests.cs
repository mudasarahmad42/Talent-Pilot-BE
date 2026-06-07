namespace TalentPilot.Tests.Auth;

public sealed class CandidateSignupInviteClaimRepositoryTests
{
    [Fact]
    public void DapperIdentityRepository_ClaimsOnlyVerifiedInvitedCandidatePlaceholders()
    {
        var repository = ReadBackendFile(
            "src",
            "TalentPilot.Infrastructure",
            "Persistence",
            "Repositories",
            "DapperIdentityRepository.cs");

        Assert.Contains("AccountStatus, \"Invited\"", repository, StringComparison.Ordinal);
        Assert.Contains("CandidateSignupStatus.InvitationInvalid", repository, StringComparison.Ordinal);
        Assert.Contains("CandidateInvitationId = @CandidateInvitationId", repository, StringComparison.Ordinal);
        Assert.Contains("JobPostId = @JobPostId", repository, StringComparison.Ordinal);
        Assert.Contains("CandidateId = @CandidateId", repository, StringComparison.Ordinal);
        Assert.Contains("UPPER(Email) = @EmailNormalized", repository, StringComparison.Ordinal);
        Assert.Contains("TokenHash = @TokenHash", repository, StringComparison.Ordinal);
        Assert.Contains("Status = N'Sent'", repository, StringComparison.Ordinal);
        Assert.Contains("ExpiresAtUtc > SYSUTCDATETIME()", repository, StringComparison.Ordinal);
        Assert.Contains("AccountStatus = N'Active'", repository, StringComparison.Ordinal);
        Assert.Contains("UPDATE dbo.UserCredentials", repository, StringComparison.Ordinal);
        Assert.Contains("UPDATE dbo.Candidates", repository, StringComparison.Ordinal);
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
