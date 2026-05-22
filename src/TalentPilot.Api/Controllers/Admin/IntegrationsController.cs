using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.Integrations;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/integrations")]
public sealed class IntegrationsController : ApiControllerBase
{
    private readonly IAdminIntegrationsService _service;

    public IntegrationsController(IAdminIntegrationsService service)
    {
        _service = service;
    }

    [HttpGet("status")]
    public async Task<ActionResult<AdminIntegrationStatusResponse>> Status(CancellationToken cancellationToken)
    {
        return FromResult(await _service.GetStatusAsync(cancellationToken));
    }
}
