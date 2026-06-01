namespace TalentPilot.Application.Operations;

public static class RecruitmentQueueVisibility
{
    public static bool CanShowAssignment(
        OperationsWorkflowAssignment assignment,
        Guid actorUserId,
        bool isTenantAdmin)
    {
        if (isTenantAdmin)
        {
            return true;
        }

        if (!string.Equals(assignment.Status, "Pending", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(assignment.Status, "Claimed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !assignment.ClaimedByUserId.HasValue || assignment.ClaimedByUserId.Value == actorUserId;
    }
}
