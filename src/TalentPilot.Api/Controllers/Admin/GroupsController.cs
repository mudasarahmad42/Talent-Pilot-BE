using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.Groups;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/groups")]
public sealed class GroupsController : AdminApiControllerBase
{
    private readonly IAdminGroupsService _service;

    public GroupsController(IAdminGroupsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<AdminGroupsResponse>> List(
        [FromQuery] string? purpose,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.ListAsync(new AdminGroupsQuery(purpose, search, page, pageSize), cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AdminGroupListItem>> Create(
        CreateGroupInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.CreateAsync(input, cancellationToken));
    }

    [HttpGet("{groupId:guid}/membership")]
    public async Task<ActionResult<AdminGroupMembershipResponse>> ListMembership(
        Guid groupId,
        [FromQuery] string? search,
        [FromQuery] string? membership,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.ListMembershipAsync(
            groupId,
            new AdminGroupMembershipQuery(search, membership, page, pageSize),
            cancellationToken));
    }

    [HttpPatch("{groupId:guid}/members")]
    public async Task<ActionResult<UpdateGroupMembersResult>> UpdateMembers(
        Guid groupId,
        UpdateGroupMembersInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.UpdateMembershipAsync(groupId, input, cancellationToken));
    }
}
