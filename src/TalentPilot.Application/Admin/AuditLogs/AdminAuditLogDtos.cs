namespace TalentPilot.Application.Admin.AuditLogs;

public sealed record AdminAuditLogQuery(
    int Page,
    int PageSize,
    string? Area,
    Guid? ActorId,
    string? Search,
    string? EntityType,
    Guid? EntityId);

public sealed record AdminAuditLogListResponse(
    AdminAuditLogSummary Summary,
    IReadOnlyList<AdminAuditLogListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminAuditLogSummary(
    int EventsToday,
    int ConfigChanges,
    int WorkflowDecisions,
    int AiEvents);

public sealed record AdminAuditLogListItem(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string ActorDisplayName,
    string EventSummary,
    string RecordLabel,
    string Area);

public sealed record AdminAuditLogDetails(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    Guid? ActorUserId,
    string ActorDisplayName,
    string EventType,
    string EntityType,
    Guid? EntityId,
    string RecordLabel,
    string EventSummary,
    string Area,
    string MetadataJson);
