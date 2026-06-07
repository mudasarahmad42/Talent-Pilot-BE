namespace TalentPilot.Application.Auth;

public sealed record LoginRequest(string Email, string? Password);

public sealed record CandidateSignupRequest(
    string? TenantSlug,
    Guid? JobPostId,
    string DisplayName,
    string Email,
    string Password,
    Guid? CandidateInvitationId = null,
    string? InvitationToken = null);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    CurrentUserContext User);

public sealed record CurrentUserContext(
    Guid UserId,
    Guid TenantId,
    string TenantDisplayName,
    string DisplayName,
    string Email,
    string RoleDisplayName,
    IReadOnlyList<CurrentUserRole> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<CurrentUserGroup> Groups,
    IReadOnlyList<string> Routes);

public sealed record CurrentUserRole(Guid RoleId, string Code, string DisplayName, int Priority);

public sealed record CurrentUserGroup(Guid GroupId, string Name, string Purpose);

public sealed record LoginOption(
    Guid UserId,
    string DisplayName,
    string Email,
    string RoleDisplayName,
    IReadOnlyList<CurrentUserRole> Roles,
    IReadOnlyList<CurrentUserGroup> Groups);
