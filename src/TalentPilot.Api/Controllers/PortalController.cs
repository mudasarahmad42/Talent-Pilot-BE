using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Operations;

namespace TalentPilot.Api.Controllers;

[AllowAnonymous]
[Route("api/portal")]
public sealed class PortalController : ApiControllerBase
{
    private readonly IOperationsService _operationsService;

    public PortalController(IOperationsService operationsService)
    {
        _operationsService = operationsService;
    }

    [HttpGet("context")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicPortalContext>> Context(
        [FromQuery] string? tenantSlug,
        [FromQuery] Guid? jobPostId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _operationsService.GetPublicPortalContextAsync(
            new PublicPortalContextQuery(tenantSlug, jobPostId),
            cancellationToken));
    }
}
