namespace TalentPilot.Application.Abstractions;

public interface ICurrentUserAccessor
{
    Guid UserId { get; }

    Guid TenantId { get; }

    string Email { get; }

    IReadOnlySet<string> RoleCodes { get; }

    IReadOnlySet<string> Permissions { get; }
}
