using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.AiSettings;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/ai-settings")]
public sealed class AiSettingsController : AdminApiControllerBase
{
    private readonly IAdminAiSettingsService _service;

    public AiSettingsController(IAdminAiSettingsService service)
    {
        _service = service;
    }

    [HttpGet("runtime")]
    public async Task<ActionResult<AdminAiRuntimeResponse>> Runtime(CancellationToken cancellationToken)
    {
        return FromResult(await _service.GetRuntimeAsync(cancellationToken));
    }

    [HttpGet("agents")]
    public async Task<ActionResult<AdminAiAgentListResponse>> Agents(CancellationToken cancellationToken)
    {
        return FromResult(await _service.GetAgentsAsync(cancellationToken));
    }

    [HttpGet("guardrails")]
    public async Task<ActionResult<AdminAiGuardrailsResponse>> Guardrails(CancellationToken cancellationToken)
    {
        return FromResult(await _service.GetGuardrailsAsync(cancellationToken));
    }
}
