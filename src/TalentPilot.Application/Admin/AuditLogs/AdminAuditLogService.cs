using System.Data;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Documents;
using TalentPilot.Common.Time;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.AuditLogs;

public sealed class AdminAuditLogService : IAdminAuditLogService
{
    private readonly IAdminAuditLogRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDocumentExportService _documentExport;
    private readonly IClock _clock;

    public AdminAuditLogService(
        IAdminAuditLogRepository repository,
        ICurrentUserAccessor currentUser,
        IDocumentExportService documentExport,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _documentExport = documentExport;
        _clock = clock;
    }

    public async Task<Result<AdminAuditLogListResponse>> ListAsync(
        AdminAuditLogQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100)
        };

        var response = await _repository.ListAsync(_currentUser.TenantId, normalized, cancellationToken);
        return Result<AdminAuditLogListResponse>.Success(response);
    }

    public async Task<Result<AdminAuditLogDetails>> GetAsync(Guid auditLogId, CancellationToken cancellationToken)
    {
        var details = await _repository.GetAsync(_currentUser.TenantId, auditLogId, cancellationToken);
        return details is null
            ? Result<AdminAuditLogDetails>.Failure("audit.not_found", "Audit log was not found.")
            : Result<AdminAuditLogDetails>.Success(details);
    }

    public async Task<Result<DocumentExportFile>> ExportAsync(
        AdminAuditLogQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(query, maxPageSize: 5_000) with { Page = 1 };
        var response = await _repository.ListAsync(_currentUser.TenantId, normalized, cancellationToken);
        var table = ToAuditLogTable(response.Items);
        var fileName = $"audit-logs-{_clock.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        var export = _documentExport.CreateExcelWorkbook(
            fileName,
            [new ExcelWorksheetData("Audit Logs", table)]);

        return Result<DocumentExportFile>.Success(export);
    }

    private static AdminAuditLogQuery Normalize(AdminAuditLogQuery query, int maxPageSize)
    {
        return query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, maxPageSize)
        };
    }

    private static DataTable ToAuditLogTable(IReadOnlyList<AdminAuditLogListItem> logs)
    {
        var table = new DataTable("Audit Logs");
        table.Columns.Add("Occurred At UTC", typeof(string));
        table.Columns.Add("Actor", typeof(string));
        table.Columns.Add("Event", typeof(string));
        table.Columns.Add("Record", typeof(string));
        table.Columns.Add("Area", typeof(string));

        foreach (var log in logs)
        {
            table.Rows.Add(
                log.OccurredAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                log.ActorDisplayName,
                log.EventSummary,
                log.RecordLabel,
                log.Area);
        }

        return table;
    }
}
