using TalentPilot.Application.Operations;

namespace TalentPilot.Tests.Operations;

public sealed class RecruitmentQueueVisibilityTests
{
    private static readonly Guid ActorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void CanShowAssignment_AllowsPendingGroupWork()
    {
        var assignment = CreateAssignment("Pending", claimedByUserId: null);

        var visible = RecruitmentQueueVisibility.CanShowAssignment(assignment, ActorUserId, isTenantAdmin: false);

        Assert.True(visible);
    }

    [Fact]
    public void CanShowAssignment_AllowsWorkClaimedByActor()
    {
        var assignment = CreateAssignment("Claimed", ActorUserId);

        var visible = RecruitmentQueueVisibility.CanShowAssignment(assignment, ActorUserId, isTenantAdmin: false);

        Assert.True(visible);
    }

    [Fact]
    public void CanShowAssignment_HidesWorkClaimedByAnotherRecruiter()
    {
        var assignment = CreateAssignment("Claimed", OtherUserId);

        var visible = RecruitmentQueueVisibility.CanShowAssignment(assignment, ActorUserId, isTenantAdmin: false);

        Assert.False(visible);
    }

    [Fact]
    public void CanShowAssignment_AllowsTenantAdminOverride()
    {
        var assignment = CreateAssignment("Claimed", OtherUserId);

        var visible = RecruitmentQueueVisibility.CanShowAssignment(assignment, ActorUserId, isTenantAdmin: true);

        Assert.True(visible);
    }

    private static OperationsWorkflowAssignment CreateAssignment(
        string status,
        Guid? claimedByUserId)
    {
        return new OperationsWorkflowAssignment(
            Guid.NewGuid(),
            "JobRequest",
            Guid.NewGuid(),
            "Recruiter Sourcing",
            "Recruiting - Delivery",
            null,
            claimedByUserId,
            status,
            DateTimeOffset.UtcNow);
    }
}
