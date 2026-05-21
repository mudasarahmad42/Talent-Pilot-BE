using System.Security.Claims;
using TalentPilot.Application.Abstractions;

namespace TalentPilot.Api.Auth;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId => ReadGuidClaim(ClaimTypes.NameIdentifier, "sub");

    public Guid TenantId => ReadGuidClaim("tenant_id");

    public string Email => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

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
}
