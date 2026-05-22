using System.Security.Claims;
using TalentPilot.Application.Abstractions;

namespace TalentPilot.Api.Auth;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly StringComparer ClaimComparer = StringComparer.OrdinalIgnoreCase;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId => ReadGuidClaim(ClaimTypes.NameIdentifier, "sub");

    public Guid TenantId => ReadGuidClaim("tenant_id");

    public string Email => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public IReadOnlySet<string> RoleCodes => ReadClaimSet(ClaimTypes.Role);

    public IReadOnlySet<string> Permissions => ReadClaimSet("permission");

    private Guid ReadGuidClaim(params string[] claimTypes)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return Guid.Empty;
        }

        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirstValue(claimType);
            if (Guid.TryParse(value, out var id))
            {
                return id;
            }
        }

        return Guid.Empty;
    }

    private IReadOnlySet<string> ReadClaimSet(string claimType)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return new HashSet<string>(ClaimComparer);
        }

        return user.FindAll(claimType)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(ClaimComparer);
    }
}
