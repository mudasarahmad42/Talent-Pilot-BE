using System.Text.Json;
using System.Text.RegularExpressions;
using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;
using TalentPilot.Domain.Tenancy;

namespace TalentPilot.Application.Admin.TenantProfiles;

public sealed class AdminTenantProfileService : IAdminTenantProfileService
{
    private static readonly Regex SlugPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);
    private static readonly Regex HexColorPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    private readonly IAdminTenantProfileRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IAdminRuntimeSettings _runtimeSettings;

    public AdminTenantProfileService(
        IAdminTenantProfileRepository repository,
        ICurrentUserAccessor currentUser,
        IAdminRuntimeSettings runtimeSettings)
    {
        _repository = repository;
        _currentUser = currentUser;
        _runtimeSettings = runtimeSettings;
    }

    public async Task<Result<TenantProfileSettings>> GetAsync(CancellationToken cancellationToken)
    {
        var profile = await _repository.GetAsync(
            _currentUser.TenantId,
            _runtimeSettings.LlmModel,
            _runtimeSettings.EmbeddingModel,
            cancellationToken);

        return profile is null
            ? Result<TenantProfileSettings>.Failure("tenant.not_found", "Tenant profile was not found.")
            : Result<TenantProfileSettings>.Success(profile);
    }

    public async Task<Result<TenantProfileSettings>> UpdateAsync(
        UpdateTenantProfileSettingsInput input,
        CancellationToken cancellationToken)
    {
        var validation = Validate(input);
        if (validation.Failed)
        {
            return Result<TenantProfileSettings>.Failure(validation.Error.Code, validation.Error.Message);
        }

        var slugAvailable = await _repository.IsSlugAvailableAsync(_currentUser.TenantId, input.Slug, cancellationToken);
        if (!slugAvailable)
        {
            return Result<TenantProfileSettings>.Failure("tenant.slug_unavailable", "Tenant slug is already in use.");
        }

        var metadataJson = JsonSerializer.Serialize(new
        {
            fields = new[]
            {
                nameof(input.DisplayName),
                nameof(input.Slug),
                nameof(input.Domain),
                nameof(input.AdminContactEmail),
                nameof(input.DefaultTimezone),
                nameof(input.DefaultCurrency),
                nameof(input.Status),
                nameof(input.CareerDisplayName),
                nameof(input.PrimaryColor),
                nameof(input.CandidateLoginRequired),
                nameof(input.CandidateCvFormat),
                nameof(input.PublicJobsEnabled),
                nameof(input.InviteExpiryDays),
                nameof(input.ReapplyCooldownDays)
            }
        });

        await _repository.UpdateAsync(_currentUser.TenantId, _currentUser.UserId, input, metadataJson, cancellationToken);
        return await GetAsync(cancellationToken);
    }

    public async Task<Result<SlugAvailabilityResponse>> CheckSlugAvailabilityAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug) || !SlugPattern.IsMatch(slug))
        {
            return Result<SlugAvailabilityResponse>.Failure("tenant.invalid_slug", "Slug must contain lowercase letters, numbers, and hyphens only.");
        }

        var available = await _repository.IsSlugAvailableAsync(_currentUser.TenantId, slug, cancellationToken);
        return Result<SlugAvailabilityResponse>.Success(new SlugAvailabilityResponse(slug, available));
    }

    private static Result Validate(UpdateTenantProfileSettingsInput input)
    {
        if (string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Trim().Length < 2)
        {
            return Result.Failure("tenant.display_name_invalid", "Display name must be at least 2 characters.");
        }

        if (string.IsNullOrWhiteSpace(input.Slug) || !SlugPattern.IsMatch(input.Slug))
        {
            return Result.Failure("tenant.slug_invalid", "Slug must contain lowercase letters, numbers, and hyphens only.");
        }

        if (string.IsNullOrWhiteSpace(input.Domain) || !input.Domain.Contains('.', StringComparison.Ordinal))
        {
            return Result.Failure("tenant.domain_invalid", "Domain must be a valid domain name.");
        }

        if (string.IsNullOrWhiteSpace(input.AdminContactEmail) || !input.AdminContactEmail.Contains('@', StringComparison.Ordinal))
        {
            return Result.Failure("tenant.email_invalid", "Admin contact email must be valid.");
        }

        if (string.IsNullOrWhiteSpace(input.DefaultTimezone) || !input.DefaultTimezone.Contains('/', StringComparison.Ordinal))
        {
            return Result.Failure("tenant.timezone_invalid", "Default timezone must be an IANA timezone id.");
        }

        if (!CurrencyCodes.Supported.Contains(input.DefaultCurrency, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure("tenant.currency_invalid", "Default currency must be PKR, USD, or EUR.");
        }

        if (input.Status is not TenantStatus.Active and not TenantStatus.Inactive)
        {
            return Result.Failure("tenant.status_invalid", "Tenant status must be Active or Inactive.");
        }

        if (!HexColorPattern.IsMatch(input.PrimaryColor))
        {
            return Result.Failure("tenant.primary_color_invalid", "Primary color must be a hex color.");
        }

        if (!string.Equals(input.CandidateCvFormat, "DOCX", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("tenant.cv_format_invalid", "MVP supports DOCX resumes only.");
        }

        if (input.InviteExpiryDays is < 1 or > 30)
        {
            return Result.Failure("tenant.invite_expiry_invalid", "Invite expiry must be between 1 and 30 days.");
        }

        if (input.ReapplyCooldownDays is < 1 or > 365)
        {
            return Result.Failure("tenant.reapply_cooldown_invalid", "Reapply cooldown must be between 1 and 365 days.");
        }

        return Result.Success();
    }
}
