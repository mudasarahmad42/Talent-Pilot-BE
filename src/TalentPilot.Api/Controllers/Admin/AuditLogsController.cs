using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Admin.AuditLogs;

namespace TalentPilot.Api.Controllers.Admin;

[Route("api/admin/audit-logs")]
public sealed class AuditLogsController : AdminApiControllerBase
{
    private readonly IAdminAuditLogService _service;

    public AuditLogsController(IAdminAuditLogService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<AdminAuditLogListResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? area = null,
        [FromQuery] Guid? actorId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new AdminAuditLogQuery(page, pageSize, area, actorId, search, entityType, entityId);
        return FromResult(await _service.ListAsync(query, cancellationToken));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? area = null,
        [FromQuery] Guid? actorId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new AdminAuditLogQuery(1, 5000, area, actorId, search, entityType, entityId);
        var result = await _service.ExportAsync(query, cancellationToken);
        if (result.Failed)
        {
            return FromResult((TalentPilot.Common.Results.Result)result);
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpGet("{auditLogId:guid}")]
    public async Task<ActionResult<AdminAuditLogDetails>> Get(Guid auditLogId, CancellationToken cancellationToken)
    {
        return FromResult(await _service.GetAsync(auditLogId, cancellationToken));
    }
}
