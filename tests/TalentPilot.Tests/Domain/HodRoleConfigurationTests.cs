using TalentPilot.Domain.Access;

namespace TalentPilot.Tests.Domain;

public sealed class HodRoleConfigurationTests
{
    [Fact]
    public void HodRoleConstant_UsesTenantScopedRoleCode()
    {
        Assert.Equal("HOD", AccessConstants.HodRoleCode);
    }

    [Fact]
    public void InitialSeed_AddsHodRoleWithInterviewerPermissions()
    {
        var seed = ReadBackendFile("scripts", "seed", "001_seed_initial_data.sql");

        Assert.Contains("N'HOD'", seed, StringComparison.Ordinal);
        Assert.Contains("N'HOD / Department Head'", seed, StringComparison.Ordinal);
        Assert.Contains("@HodRoleId, N'workflow.assignments.claim'", seed, StringComparison.Ordinal);
        Assert.Contains("@HodRoleId, N'interviews.manage'", seed, StringComparison.Ordinal);
        Assert.DoesNotContain("@HodRoleId, N'hiring.decisions.manage'", seed, StringComparison.Ordinal);
    }

    [Fact]
    public void DomainSeed_DefaultsDepartmentHeadRoundToHodUser()
    {
        var seed = ReadBackendFile("scripts", "seed", "002_seed_domain_reference_data.sql");

        Assert.Contains("@HodEmployeeId", seed, StringComparison.Ordinal);
        Assert.Contains("N'Zara Siddiqui'", seed, StringComparison.Ordinal);
        Assert.Contains("N'Department Head Interview', @HodRoleId, @HodUserId", seed, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_BackfillsHodRoleAndFinalRoundDefaults()
    {
        var migration = ReadBackendFile("scripts", "migrations", "021_add_hod_role_and_demo_user.sql");

        Assert.Contains("HOD / Department Head", migration, StringComparison.Ordinal);
        Assert.Contains("N'Head of Engineering'", migration, StringComparison.Ordinal);
        Assert.Contains("OwnerRoleId = @HodRoleId", migration, StringComparison.Ordinal);
        Assert.Contains("OwnerUserId = @HodUserId", migration, StringComparison.Ordinal);
        Assert.Contains("HOD participates as an interviewer", migration, StringComparison.Ordinal);
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
