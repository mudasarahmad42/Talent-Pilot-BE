namespace TalentPilot.Domain.Access;

public sealed class EffectivePermissionResolver
{
    public IReadOnlyList<string> Resolve(IEnumerable<RoleGrant> roles, PermissionResolutionMode mode)
    {
        var roleList = roles
            .OrderBy(role => role.Priority)
            .ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roleList.Count == 0)
        {
            return Array.Empty<string>();
        }

        var selectedRoles = mode == PermissionResolutionMode.HighestPriorityRoleOnly
            ? roleList.Take(1)
            : roleList;

        return selectedRoles
            .SelectMany(role => role.PermissionIds)
            .Where(permissionId => !string.IsNullOrWhiteSpace(permissionId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public RoleGrant? ResolveDisplayRole(IEnumerable<RoleGrant> roles)
    {
        return roles
            .OrderBy(role => role.Priority)
            .ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
