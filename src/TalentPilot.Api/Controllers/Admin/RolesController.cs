using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.Roles;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/roles")]
public sealed class RolesController : AdminApiControllerBase
{
    private readonly IAdminRolesService _service;

    public RolesController(IAdminRolesService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<AdminRolesResponse>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.ListAsync(new AdminRolesQuery(search, page, pageSize, includeInactive), cancellationToken));
    }

    [HttpGet("{roleId:guid}")]
    public async Task<ActionResult<RoleDetails>> Get(Guid roleId, CancellationToken cancellationToken)
    {
        return FromResult(await _service.GetAsync(roleId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<RoleDetails>> Create(SaveRoleInput input, CancellationToken cancellationToken)
    {
        return FromResult(await _service.CreateAsync(input, cancellationToken));
    }

    [HttpPut("{roleId:guid}")]
    public async Task<ActionResult<RoleDetails>> Update(
        Guid roleId,
        SaveRoleInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.UpdateAsync(roleId, input, cancellationToken));
    }

    [HttpPatch("{roleId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid roleId,
        UpdateRoleStatusInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.UpdateStatusAsync(roleId, input, cancellationToken));
    }

    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyList<PermissionCatalogItem>>> ListPermissions(CancellationToken cancellationToken)
    {
        return FromResult(await _service.ListPermissionsAsync(cancellationToken));
    }

    [HttpPost("{roleId:guid}/user-assignment-preview")]
    public async Task<ActionResult<RoleUserAssignmentPreview>> PreviewAssignments(
        Guid roleId,
        RoleUserAssignmentFilterInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.PreviewUserAssignmentsAsync(roleId, input, cancellationToken));
    }

    [HttpPost("{roleId:guid}/bulk-user-assignments")]
    public async Task<ActionResult<BulkAssignRoleUsersResponse>> BulkAssign(
        Guid roleId,
        BulkAssignRoleUsersInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.BulkAssignUsersAsync(roleId, input, cancellationToken));
    }
}
