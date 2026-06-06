using System.Security.Claims;
using TalentPilot.Application.Abstractions;

namespace TalentPilot.Api.Auth;

public interface ICurrentUserContextOverride
{
    void Set(Guid tenantId, Guid userId, string email);

    void Clear();
}

public sealed class CurrentUserAccessor : ICurrentUserAccessor, ICurrentUserContextOverride
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _tenantIdOverride;
    private Guid? _userIdOverride;
    private string? _emailOverride;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId => _userIdOverride ?? ReadGuidClaim(ClaimTypes.NameIdentifier, "sub");

    public Guid TenantId => _tenantIdOverride ?? ReadGuidClaim("tenant_id");

    public string Email => _emailOverride ?? _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public IReadOnlyCollection<string> RoleCodes =>
        _httpContextAccessor.HttpContext?.User
            .FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? Array.Empty<string>();

    public void Set(Guid tenantId, Guid userId, string email)
    {
        _tenantIdOverride = tenantId;
        _userIdOverride = userId;
        _emailOverride = email;
    }

    public void Clear()
    {
        _tenantIdOverride = null;
        _userIdOverride = null;
        _emailOverride = null;
    }

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
