using TalentPilot.Domain.Access;

namespace TalentPilot.Tests.Domain;

public sealed class EffectivePermissionResolverTests
{
    private readonly EffectivePermissionResolver _resolver = new();

    [Fact]
    public void Resolve_WhenPolicyMergesRoles_ReturnsDistinctPermissionsFromEveryAssignedRole()
    {
        var roles = new[]
        {
            new RoleGrant(Guid.NewGuid(), "Recruiter", 30, new HashSet<string>(["candidates.manage", "jobs.view"])),
            new RoleGrant(Guid.NewGuid(), "PMO", 20, new HashSet<string>(["jobs.view", "bench.review"]))
        };

        var permissions = _resolver.Resolve(roles, PermissionResolutionMode.MergeAllAssignedRoles);

        Assert.Equal(["bench.review", "candidates.manage", "jobs.view"], permissions);
    }

    [Fact]
    public void Resolve_WhenPolicyUsesHighestPriorityRole_ReturnsOnlyLowestPriorityNumberPermissions()
    {
        var roles = new[]
        {
            new RoleGrant(Guid.NewGuid(), "Recruiter", 30, new HashSet<string>(["candidates.manage"])),
            new RoleGrant(Guid.NewGuid(), "Tenant Admin", 1, new HashSet<string>(["access.admin.manage"]))
        };

        var permissions = _resolver.Resolve(roles, PermissionResolutionMode.HighestPriorityRoleOnly);

        Assert.Equal(["access.admin.manage"], permissions);
    }

    [Fact]
    public void ResolveDisplayRole_ReturnsHighestPriorityRoleWithoutChangingTheName()
    {
        var role = new RoleGrant(Guid.NewGuid(), "Tenant Admin", 1, new HashSet<string>(["access.admin.manage"]));
        var roles = new[]
        {
            new RoleGrant(Guid.NewGuid(), "Recruiter", 30, new HashSet<string>(["candidates.manage"])),
            role
        };

        var displayRole = _resolver.ResolveDisplayRole(roles);

        Assert.NotNull(displayRole);
        Assert.Equal("Tenant Admin", displayRole.Name);
    }
}
