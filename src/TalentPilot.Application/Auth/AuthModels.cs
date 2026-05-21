using TalentPilot.Domain.Access;

namespace TalentPilot.Application.Auth;

public sealed class AuthRuntimeOptions
{
    public bool AllowDemoCardLogin { get; init; }

    public int AccessTokenMinutes { get; init; } = 60;

    public int RefreshTokenDays { get; init; } = 7;
}

public sealed class AuthUserRecord
{
    public Guid UserId { get; init; }

    public Guid TenantId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string AccountStatus { get; init; } = string.Empty;

    public string? PasswordHash { get; init; }
}

public sealed class CurrentUserData
{
    public Guid UserId { get; init; }

    public Guid TenantId { get; init; }

    public string TenantDisplayName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public PermissionResolutionMode PermissionResolutionMode { get; init; }

    public List<RoleWithPermissions> Roles { get; } = [];

    public List<CurrentUserGroup> Groups { get; } = [];
}

public sealed class RoleWithPermissions
{
    public Guid RoleId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Priority { get; init; }

    public HashSet<string> PermissionIds { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RefreshTokenRecord
{
    public Guid RefreshTokenId { get; init; }

    public Guid TenantId { get; init; }

    public Guid UserId { get; init; }

    public string TokenHash { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public DateTimeOffset? RevokedAtUtc { get; init; }
}

public sealed record JwtTokenResult(string AccessToken, DateTimeOffset ExpiresAtUtc);
