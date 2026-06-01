namespace TalentPilot.Application.Admin.TenantProfiles;

public sealed record TenantProfileSettings(
    Guid TenantId,
    string DisplayName,
    string Slug,
    string Domain,
    string AdminContactEmail,
    string DefaultTimezone,
    string DefaultCurrency,
    string Status,
    string CareerDisplayName,
    string PrimaryColor,
    bool CandidateLoginRequired,
    string CandidateCvFormat,
    bool PublicJobsEnabled,
    int InviteExpiryDays,
    int ReapplyCooldownDays,
    int UserCount,
    int RoleCount,
    bool SetupComplete,
    string ConfiguredLlmModel,
    string ConfiguredEmbeddingModel,
    string? LogoFileName,
    string? LogoContentType,
    string? LogoContentBase64,
    DateTimeOffset UpdatedAt);

public sealed record UpdateTenantProfileSettingsInput(
    string DisplayName,
    string Slug,
    string Domain,
    string AdminContactEmail,
    string DefaultTimezone,
    string DefaultCurrency,
    string Status,
    string CareerDisplayName,
    string PrimaryColor,
    bool CandidateLoginRequired,
    string CandidateCvFormat,
    bool PublicJobsEnabled,
    int InviteExpiryDays,
    int ReapplyCooldownDays,
    string? LogoFileName,
    string? LogoContentType,
    string? LogoContentBase64);

public sealed record SlugAvailabilityResponse(string Slug, bool Available);
