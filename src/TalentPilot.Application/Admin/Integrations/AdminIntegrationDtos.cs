namespace TalentPilot.Application.Admin.Integrations;

public sealed record AdminIntegrationStatusResponse(
    bool ReadOnly,
    int TotalCount,
    IReadOnlyList<AdminIntegrationStatusItem> Items);

public sealed record AdminIntegrationStatusItem(
    string Id,
    string DisplayName,
    string Category,
    string Status,
    bool Enabled,
    bool Editable,
    string RuntimeMode,
    string DeliveryPath,
    string MvpContract,
    IReadOnlyList<AdminIntegrationMetric> Metrics);

public sealed record AdminIntegrationMetric(
    string Name,
    int Value);
