using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.CandidateSources;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/candidate-sources")]
public sealed class CandidateSourcesController : AdminApiControllerBase
{
    private readonly IAdminCandidateSourcesService _service;

    public CandidateSourcesController(IAdminCandidateSourcesService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<AdminCandidateSourcesResponse>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.ListAsync(new AdminCandidateSourcesQuery(search, page, pageSize), cancellationToken));
    }
}
