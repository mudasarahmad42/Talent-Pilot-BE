namespace TalentPilot.Domain.Access;

public enum PermissionResolutionMode
{
    MergeAllAssignedRoles = 0,
    HighestPriorityRoleOnly = 1
}
