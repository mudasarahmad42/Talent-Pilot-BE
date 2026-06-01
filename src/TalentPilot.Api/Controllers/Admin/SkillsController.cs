using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.Skills;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/skills")]
public sealed class SkillsController : AdminApiControllerBase
{
    private readonly IAdminSkillsService _service;

    public SkillsController(IAdminSkillsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<AdminSkillsResponse>> List(
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.ListAsync(new AdminSkillsQuery(category, search, page, pageSize), cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AdminSkillListItem>> Create(
        CreateSkillInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.CreateAsync(input, cancellationToken));
    }

    [HttpPut("{skillId:guid}")]
    public async Task<ActionResult<AdminSkillListItem>> Update(
        Guid skillId,
        UpdateSkillInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.UpdateAsync(skillId, input, cancellationToken));
    }

    [HttpDelete("{skillId:guid}")]
    public async Task<IActionResult> Delete(
        Guid skillId,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.DeleteAsync(skillId, cancellationToken));
    }
}
