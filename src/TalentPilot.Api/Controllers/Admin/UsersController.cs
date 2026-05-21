using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.Users;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/users")]
public sealed class UsersController : ApiControllerBase
{
    private readonly IAdminUsersService _usersService;

    public UsersController(IAdminUsersService usersService)
    {
        _usersService = usersService;
    }

    [HttpGet]
    public async Task<ActionResult<AdminUsersResponse>> List(
        [FromQuery] string? search,
        [FromQuery] Guid? roleId,
        [FromQuery] Guid? groupId,
        [FromQuery] string? accountStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = new AdminUsersQuery(search, roleId, groupId, accountStatus, page, pageSize);
        return FromResult(await _usersService.ListAsync(query, cancellationToken));
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<AdminUserDetails>> Get(Guid userId, CancellationToken cancellationToken)
    {
        return FromResult(await _usersService.GetAsync(userId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserDetails>> Create(SaveAdminUserInput input, CancellationToken cancellationToken)
    {
        return FromResult(await _usersService.CreateAsync(input, cancellationToken));
    }

    [HttpPut("{userId:guid}")]
    public async Task<ActionResult<AdminUserDetails>> Update(
        Guid userId,
        SaveAdminUserInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _usersService.UpdateAsync(userId, input, cancellationToken));
    }

    [HttpPatch("{userId:guid}/account-status")]
    public async Task<IActionResult> UpdateStatus(
        Guid userId,
        UpdateAdminUserStatusInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _usersService.UpdateStatusAsync(userId, input, cancellationToken));
    }

    [HttpPost("{userId:guid}/invites/resend")]
    public async Task<IActionResult> ResendInvite(Guid userId, CancellationToken cancellationToken)
    {
        return FromResult(await _usersService.ResendInviteAsync(userId, cancellationToken));
    }
}
