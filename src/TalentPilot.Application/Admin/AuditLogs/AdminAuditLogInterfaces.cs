using TalentPilot.Application.Documents;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.AuditLogs;

public interface IAdminAuditLogService
{
    Task<Result<AdminAuditLogListResponse>> ListAsync(AdminAuditLogQuery query, CancellationToken cancellationToken);

    Task<Result<AdminAuditLogDetails>> GetAsync(Guid auditLogId, CancellationToken cancellationToken);

    Task<Result<DocumentExportFile>> ExportAsync(AdminAuditLogQuery query, CancellationToken cancellationToken);
}

public interface IAdminAuditLogRepository
{
    Task<AdminAuditLogListResponse> ListAsync(Guid tenantId, AdminAuditLogQuery query, CancellationToken cancellationToken);

    Task<AdminAuditLogDetails?> GetAsync(Guid tenantId, Guid auditLogId, CancellationToken cancellationToken);
}
