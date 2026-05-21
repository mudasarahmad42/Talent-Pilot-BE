namespace TalentPilot.Domain.Access;

public sealed record RoleGrant(
    Guid RoleId,
    string Name,
    int Priority,
    IReadOnlySet<string> PermissionIds);
