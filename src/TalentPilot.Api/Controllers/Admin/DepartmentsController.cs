using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.Departments;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/departments")]
public sealed class DepartmentsController : AdminApiControllerBase
{
    private readonly IAdminDepartmentsService _service;

    public DepartmentsController(IAdminDepartmentsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<AdminDepartmentsResponse>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.ListAsync(new AdminDepartmentsQuery(search, page, pageSize), cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AdminDepartmentListItem>> Create(
        CreateDepartmentInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _service.CreateAsync(input, cancellationToken));
    }
}
