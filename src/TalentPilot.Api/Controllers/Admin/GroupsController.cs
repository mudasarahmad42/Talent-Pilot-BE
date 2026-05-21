using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.Groups;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/groups")]
public sealed class GroupsController : ApiControllerBase
{
    private readonly IAdminGroupsService _service;

    public GroupsController(IAdminGroupsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<AdminGroupsResponse>> List(
        [FromQuery] string? purpose,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.ListAsync(new AdminGroupsQuery(purpose, page, pageSize), cancellationToken));
    }
}
