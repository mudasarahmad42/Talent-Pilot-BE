using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.Roles;
using TalentPilot.Application.Admin.Users;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/access-policies")]
public sealed class AccessPoliciesController : AdminApiControllerBase
{
    private readonly IAdminAccessPoliciesService _accessPoliciesService;
    private readonly IAdminRolesService _rolesService;

    public AccessPoliciesController(
        IAdminAccessPoliciesService accessPoliciesService,
        IAdminRolesService rolesService)
    {
        _accessPoliciesService = accessPoliciesService;
        _rolesService = rolesService;
    }

    [HttpGet("bench-visibility")]
    public async Task<ActionResult<BenchVisibilityPolicy>> GetBenchVisibility(CancellationToken cancellationToken)
    {
        return FromResult(await _accessPoliciesService.GetBenchVisibilityPolicyAsync(cancellationToken));
    }

    [HttpPut("bench-visibility")]
    public async Task<ActionResult<BenchVisibilityPolicy>> UpdateBenchVisibility(
        UpdateBenchVisibilityPolicyInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _accessPoliciesService.UpdateBenchVisibilityPolicyAsync(input, cancellationToken));
    }

    [HttpGet("permission-resolution")]
    public async Task<ActionResult<PermissionResolutionPolicy>> GetPermissionResolution(CancellationToken cancellationToken)
    {
        return FromResult(await _rolesService.GetPermissionResolutionPolicyAsync(cancellationToken));
    }

    [HttpPut("permission-resolution")]
    public async Task<ActionResult<PermissionResolutionPolicy>> UpdatePermissionResolution(
        UpdatePermissionResolutionPolicyInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _rolesService.UpdatePermissionResolutionPolicyAsync(input, cancellationToken));
    }
}
