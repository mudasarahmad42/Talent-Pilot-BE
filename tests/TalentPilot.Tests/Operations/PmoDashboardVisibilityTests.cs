using TalentPilot.Application.Operations;

namespace TalentPilot.Tests.Operations;

public sealed class PmoDashboardVisibilityTests
{
    private static readonly Guid ActorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void CanShowAssignment_AllowsPendingGroupWorkWhenActorCanClaim()
    {
        var visible = PmoDashboardVisibility.CanShowAssignment(
            "Pending",
            assignedToUserId: null,
            claimedByUserId: null,
            actorCanClaimOrOwnPendingWork: true,
            ActorUserId,
            isTenantAdmin: false);

        Assert.True(visible);
    }

    [Fact]
    public void CanShowAssignment_AllowsPendingDirectAssignment()
    {
        var visible = PmoDashboardVisibility.CanShowAssignment(
            "Pending",
            ActorUserId,
            claimedByUserId: null,
            actorCanClaimOrOwnPendingWork: false,
            ActorUserId,
            isTenantAdmin: false);

        Assert.True(visible);
    }

    [Fact]
    public void CanShowAssignment_AllowsWorkClaimedByActor()
    {
        var visible = PmoDashboardVisibility.CanShowAssignment(
            "Claimed",
            assignedToUserId: null,
            ActorUserId,
            actorCanClaimOrOwnPendingWork: false,
            ActorUserId,
            isTenantAdmin: false);

        Assert.True(visible);
    }

    [Fact]
    public void CanShowAssignment_HidesWorkClaimedByAnotherPmo()
    {
        var visible = PmoDashboardVisibility.CanShowAssignment(
            "Claimed",
            assignedToUserId: null,
            OtherUserId,
            actorCanClaimOrOwnPendingWork: true,
            ActorUserId,
            isTenantAdmin: false);

        Assert.False(visible);
    }

    [Fact]
    public void CanShowAssignment_AllowsTenantAdminOverride()
    {
        var visible = PmoDashboardVisibility.CanShowAssignment(
            "Claimed",
            assignedToUserId: null,
            OtherUserId,
            actorCanClaimOrOwnPendingWork: false,
            ActorUserId,
            isTenantAdmin: true);

        Assert.True(visible);
    }
}
