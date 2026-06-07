using System.Reflection;
using TalentPilot.Infrastructure.Persistence.Repositories;

namespace TalentPilot.Tests.Operations;

public sealed class CandidateInvitationLinkTests
{
    [Fact]
    public void CandidateInvitationLink_IncludesTrackingParameters()
    {
        var jobPostId = Guid.Parse("24000000-0000-0000-0000-000000000001");
        var invitationId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        const string token = "tracked-token";
        var link = InvokeBuildCandidateInvitationLink(
            $"https://talentpilot-demo.duckdns.org/candidate/jobs/{jobPostId:D}?source=invite",
            jobPostId,
            invitationId,
            token);

        Assert.NotNull(link);
        Assert.StartsWith(
            $"https://talentpilot-demo.duckdns.org/candidate/jobs/{jobPostId:D}",
            link,
            StringComparison.Ordinal);
        Assert.Contains("source=invite", link, StringComparison.Ordinal);
        Assert.Contains($"inviteId={invitationId:D}", link, StringComparison.Ordinal);
        Assert.Contains("token=tracked-token", link, StringComparison.Ordinal);
    }

    [Fact]
    public void InviteEmailPaths_FallBackToConfiguredFrontendBaseUrl()
    {
        var repository = ReadBackendFile(
            "src",
            "TalentPilot.Infrastructure",
            "Persistence",
            "Repositories",
            "DapperOperationsRepository.cs");

        Assert.Contains(
            "ExtractFirstAbsoluteUrl(recruiterMessage) ?? configuredJobLink",
            repository,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExtractFirstAbsoluteUrl(invitationText) ?? configuredJobLink",
            repository,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionCompose_ProvidesFrontendBaseUrlToApi()
    {
        var compose = ReadBackendFile("docker-compose.prod.yml");

        Assert.Contains(
            "Frontend__BaseUrl: ${FRONTEND_BASE_URL:-https://talentpilot-demo.duckdns.org}",
            compose,
            StringComparison.Ordinal);
    }

    private static string? InvokeBuildCandidateInvitationLink(
        string baseJobLink,
        Guid jobPostId,
        Guid invitationId,
        string token)
    {
        var method = typeof(DapperOperationsRepository).GetMethod(
            "BuildCandidateInvitationLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return (string?)method!.Invoke(null, [baseJobLink, jobPostId, invitationId, token]);
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
