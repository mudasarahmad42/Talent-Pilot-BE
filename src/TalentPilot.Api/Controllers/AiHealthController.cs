using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Admin.AiSettings;
using TalentPilot.Common.Results;

namespace TalentPilot.Api.Controllers;

[Authorize]
[Route("api/ai-health")]
public sealed class AiHealthController : ApiControllerBase
{
    private readonly IAiModelHealthChecker _aiModelHealthChecker;

    public AiHealthController(IAiModelHealthChecker aiModelHealthChecker)
    {
        _aiModelHealthChecker = aiModelHealthChecker;
    }

    [HttpGet("llm")]
    public async Task<ActionResult<AiHealthStatusResponse>> Llm(CancellationToken cancellationToken)
    {
        var health = await _aiModelHealthChecker.CheckAsync(cancellationToken);
        return FromResult(Result<AiHealthStatusResponse>.Success(
            new AiHealthStatusResponse(health.IsAvailable, health.Status, health.Message)));
    }
}
