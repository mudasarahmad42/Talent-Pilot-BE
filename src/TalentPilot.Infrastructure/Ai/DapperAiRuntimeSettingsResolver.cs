using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Admin.AiSettings;

namespace TalentPilot.Infrastructure.Ai;

public sealed class DapperAiRuntimeSettingsResolver : IAiRuntimeSettingsResolver
{
    private readonly IAdminAiSettingsRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IAdminRuntimeSettings _fallbackSettings;

    public DapperAiRuntimeSettingsResolver(
        IAdminAiSettingsRepository repository,
        ICurrentUserAccessor currentUser,
        IAdminRuntimeSettings fallbackSettings)
    {
        _repository = repository;
        _currentUser = currentUser;
        _fallbackSettings = fallbackSettings;
    }

    public async Task<AiRuntimeSettingsSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var tenantRuntime = await _repository.GetRuntimeAsync(_currentUser.TenantId, cancellationToken);

        return new AiRuntimeSettingsSnapshot(
            _currentUser.TenantId,
            tenantRuntime?.Provider ?? _fallbackSettings.Provider,
            tenantRuntime?.LlmModel ?? _fallbackSettings.LlmModel,
            tenantRuntime?.EmbeddingModel ?? _fallbackSettings.EmbeddingModel,
            tenantRuntime?.EmbeddingDimensions ?? _fallbackSettings.EmbeddingDimensions,
            tenantRuntime?.VectorStore ?? _fallbackSettings.VectorStore,
            _fallbackSettings.OllamaBaseUrl);
    }
}
