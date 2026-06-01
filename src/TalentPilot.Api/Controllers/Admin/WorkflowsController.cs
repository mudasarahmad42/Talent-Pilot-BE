using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.Workflows;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/workflows")]
public sealed class WorkflowsController : AdminApiControllerBase
{
    private readonly IAdminWorkflowsService _service;

    public WorkflowsController(IAdminWorkflowsService service)
    {
        _service = service;
    }

    [HttpGet("configuration")]
    public async Task<ActionResult<AdminWorkflowConfigurationResponse>> GetConfiguration(
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.GetConfigurationAsync(cancellationToken));
    }

    [HttpPut("intake-routing")]
    public async Task<ActionResult<AdminWorkflowConfigurationResponse>> UpdateIntakeRouting(
        UpdateAdminWorkflowIntakeRoutingInput input,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.UpdateIntakeRoutingAsync(input, cancellationToken));
    }
}
