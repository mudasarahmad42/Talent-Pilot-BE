using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.TenantProfiles;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/tenant-profile")]
public sealed class TenantProfileController : AdminApiControllerBase
{
    private readonly IAdminTenantProfileService _service;

    public TenantProfileController(IAdminTenantProfileService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<TenantProfileSettings>> Get(CancellationToken cancellationToken)
    {
        return FromResult(await _service.GetAsync(cancellationToken));
    }

    [HttpPut]
    public async Task<ActionResult<TenantProfileSettings>> Update(
        UpdateTenantProfileSettingsInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.UpdateAsync(input, cancellationToken));
    }

    [HttpGet("slug-availability")]
    public async Task<ActionResult<SlugAvailabilityResponse>> SlugAvailability(
        [FromQuery] string slug,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.CheckSlugAvailabilityAsync(slug, cancellationToken));
    }
}
