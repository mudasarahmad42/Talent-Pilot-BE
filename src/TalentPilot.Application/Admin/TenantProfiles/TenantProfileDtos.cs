namespace TalentPilot.Application.Admin.TenantProfiles;

public static class AdminCenterAccessModes
{
    public const string FullAccess = "FullAccess";
    public const string ReadOnly = "ReadOnly";

    public static bool IsSupported(string value)
    {
        return string.Equals(value, FullAccess, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, ReadOnly, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string? value)
    {
        return string.Equals(value, ReadOnly, StringComparison.OrdinalIgnoreCase)
            ? ReadOnly
            : FullAccess;
    }
}

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
    string? CompanyAddress,
    string? CompanyCity,
    string? CompanyCountry,
    string? OfficialEmail,
    string? OfficialPhone,
    string PrimaryColor,
    bool CandidateLoginRequired,
    string CandidateCvFormat,
    bool PublicJobsEnabled,
    int InviteExpiryDays,
    int ReapplyCooldownDays,
    string NotificationEmailProvider,
    string AdminCenterAccessMode,
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
    string? CompanyAddress,
    string? CompanyCity,
    string? CompanyCountry,
    string? OfficialEmail,
    string? OfficialPhone,
    string PrimaryColor,
    bool CandidateLoginRequired,
    string CandidateCvFormat,
    bool PublicJobsEnabled,
    int InviteExpiryDays,
    int ReapplyCooldownDays,
    string NotificationEmailProvider,
    string AdminCenterAccessMode,
    string? LogoFileName,
    string? LogoContentType,
    string? LogoContentBase64);

public sealed record SlugAvailabilityResponse(string Slug, bool Available);
