using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.HiringPipelines;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/hiring-pipeline")]
public sealed class HiringPipelineController : AdminApiControllerBase
{
    private readonly IAdminHiringPipelinesService _service;

    public HiringPipelineController(IAdminHiringPipelinesService service)
    {
        _service = service;
    }

    [HttpGet("templates")]
    public async Task<ActionResult<AdminHiringPipelineTemplatesResponse>> ListTemplates(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.ListTemplatesAsync(
            new AdminHiringPipelineTemplatesQuery(search, page, pageSize),
            cancellationToken));
    }

    [HttpGet("templates/{templateId:guid}")]
    public async Task<ActionResult<AdminHiringPipelineTemplateDetails>> GetTemplate(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.GetTemplateAsync(templateId, cancellationToken));
    }

    [HttpPost("templates")]
    public async Task<ActionResult<AdminHiringPipelineTemplateDetails>> CreateTemplate(
        CreateAdminHiringPipelineTemplateInput input,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.CreateTemplateAsync(input, cancellationToken));
    }

    [HttpPut("templates/{templateId:guid}")]
    public async Task<ActionResult<AdminHiringPipelineTemplateDetails>> UpdateTemplate(
        Guid templateId,
        UpdateAdminHiringPipelineTemplateInput input,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await _service.UpdateTemplateAsync(templateId, input, cancellationToken));
    }
}
