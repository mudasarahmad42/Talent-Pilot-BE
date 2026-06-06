using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Auth;

namespace TalentPilot.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("login-options")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<LoginOption>>> LoginOptions(CancellationToken cancellationToken)
    {
        return FromResult(await _authService.ListLoginOptionsAsync(cancellationToken));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _authService.LoginAsync(request, cancellationToken));
    }

    [HttpPost("candidate-signup")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> CandidateSignup(
        CandidateSignupRequest request,
        CancellationToken cancellationToken)
    {
        return FromResult(await _authService.RegisterCandidateAsync(request, cancellationToken));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _authService.RefreshAsync(request, cancellationToken));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _authService.LogoutAsync(request, cancellationToken));
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserContext>> Me(CancellationToken cancellationToken)
    {
        var tenantId = ReadGuidClaim("tenant_id");
        var userId = ReadGuidClaim(ClaimTypes.NameIdentifier, "sub");
        return FromResult(await _authService.GetCurrentUserAsync(tenantId, userId, cancellationToken));
    }

    private Guid ReadGuidClaim(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = User.FindFirstValue(claimType);
            if (Guid.TryParse(value, out var id))
            {
                return id;
            }
        }

        return Guid.Empty;
    }
}
