namespace TalentPilot.Application.Operations;

public static class PmoDashboardVisibility
{
    public static bool CanShowAssignment(
        string status,
        Guid? assignedToUserId,
        Guid? claimedByUserId,
        bool actorCanClaimOrOwnPendingWork,
        Guid actorUserId,
        bool isTenantAdmin)
    {
        if (isTenantAdmin)
        {
            return true;
        }

        if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return assignedToUserId == actorUserId || actorCanClaimOrOwnPendingWork;
        }

        if (string.Equals(status, "Claimed", StringComparison.OrdinalIgnoreCase))
        {
            return claimedByUserId == actorUserId || assignedToUserId == actorUserId;
        }

        return false;
    }
}
