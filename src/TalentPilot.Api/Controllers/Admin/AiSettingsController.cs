using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.AiSettings;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/ai-settings")]
public sealed class AiSettingsController : ApiControllerBase
{
    private readonly IAdminAiSettingsService _service;

    public AiSettingsController(IAdminAiSettingsService service)
    {
        _service = service;
    }

    [HttpGet("runtime")]
    public ActionResult<AdminAiRuntimeResponse> Runtime()
    {
        return FromResult(_service.GetRuntime());
    }

    [HttpGet("agents")]
    public ActionResult<AdminAiAgentListResponse> Agents()
    {
        return FromResult(_service.GetAgents());
    }

    [HttpGet("guardrails")]
    public ActionResult<AdminAiGuardrailsResponse> Guardrails()
    {
        return FromResult(_service.GetGuardrails());
    }
}
