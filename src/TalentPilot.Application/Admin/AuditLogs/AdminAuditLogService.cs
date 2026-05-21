using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.AuditLogs;

public sealed class AdminAuditLogService : IAdminAuditLogService
{
    private readonly IAdminAuditLogRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminAuditLogService(IAdminAuditLogRepository repository, ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
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
}
