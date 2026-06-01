using System.Text.Json;
using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Skills;

public sealed class AdminSkillsService : IAdminSkillsService
{
    private static readonly string[] ValidStatuses = ["Active", "Inactive"];

    private readonly IAdminSkillsRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminSkillsService(IAdminSkillsRepository repository, ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminSkillsResponse>> ListAsync(AdminSkillsQuery query, CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Category = string.IsNullOrWhiteSpace(query.Category) ? null : query.Category.Trim(),
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100)
        };

        var response = await _repository.ListAsync(_currentUser.TenantId, normalized, cancellationToken);
        return Result<AdminSkillsResponse>.Success(response);
    }

    public async Task<Result<AdminSkillListItem>> CreateAsync(CreateSkillInput input, CancellationToken cancellationToken)
    {
        var validation = await ValidateSkillInputAsync(
            input.Name,
            input.Category,
            input.Aliases,
            input.Status,
            currentNormalizedName: null,
            cancellationToken);
        if (validation.Failed)
        {
            return Result<AdminSkillListItem>.Failure(validation.Error.Code, validation.Error.Message);
        }

        var aliases = NormalizeAliases(input.Aliases);
        var normalized = new CreateSkillInput(
            input.Name.Trim(),
            input.Category.Trim(),
            aliases,
            string.IsNullOrWhiteSpace(input.Status) ? "Active" : input.Status.Trim());
        var normalizedName = NormalizeSkillName(normalized.Name);
        var aliasesJson = JsonSerializer.Serialize(aliases);
        var metadataJson = JsonSerializer.Serialize(new
        {
            action = "create",
            normalized.Name,
            normalizedName,
            normalized.Category,
            aliases,
            normalized.Status
        });

        var skillId = await _repository.CreateAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            normalized,
            normalizedName,
            aliasesJson,
            metadataJson,
            cancellationToken);

        var skill = await _repository.GetSkillAsync(_currentUser.TenantId, skillId, cancellationToken);
        return skill is null
            ? Result<AdminSkillListItem>.Failure("admin_skills.not_found", "Skill was created but could not be loaded.")
            : Result<AdminSkillListItem>.Success(skill);
    }

    public async Task<Result<AdminSkillListItem>> UpdateAsync(
        Guid skillId,
        UpdateSkillInput input,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetSkillAsync(_currentUser.TenantId, skillId, cancellationToken);
        if (existing is null)
        {
            return Result<AdminSkillListItem>.Failure("admin_skills.not_found", "Skill was not found.");
        }

        var validation = await ValidateSkillInputAsync(
            input.Name,
            input.Category,
            input.Aliases,
            input.Status,
            existing.NormalizedName,
            cancellationToken);
        if (validation.Failed)
        {
            return Result<AdminSkillListItem>.Failure(validation.Error.Code, validation.Error.Message);
        }

        var aliases = NormalizeAliases(input.Aliases);
        var normalized = new UpdateSkillInput(
            input.Name.Trim(),
            input.Category.Trim(),
            aliases,
            string.IsNullOrWhiteSpace(input.Status) ? "Active" : input.Status.Trim());
        var normalizedName = NormalizeSkillName(normalized.Name);
        var aliasesJson = JsonSerializer.Serialize(aliases);
        var metadataJson = JsonSerializer.Serialize(new
        {
            action = "update",
            skillId,
            previousName = existing.Name,
            normalized.Name,
            normalizedName,
            normalized.Category,
            aliases,
            normalized.Status
        });

        await _repository.UpdateAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            skillId,
            normalized,
            normalizedName,
            aliasesJson,
            metadataJson,
            cancellationToken);

        var skill = await _repository.GetSkillAsync(_currentUser.TenantId, skillId, cancellationToken);
        return skill is null
            ? Result<AdminSkillListItem>.Failure("admin_skills.not_found", "Skill was updated but could not be loaded.")
            : Result<AdminSkillListItem>.Success(skill);
    }

    public async Task<Result> DeleteAsync(Guid skillId, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetSkillAsync(_currentUser.TenantId, skillId, cancellationToken);
        if (existing is null)
        {
            return Result.Failure("admin_skills.not_found", "Skill was not found.");
        }

        var metadataJson = JsonSerializer.Serialize(new
        {
            action = "delete",
            skillId,
            existing.Name,
            existing.NormalizedName,
            existing.Category,
            existing.Aliases,
            existing.Status
        });

        await _repository.DeleteAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            skillId,
            existing.Name,
            metadataJson,
            cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ValidateSkillInputAsync(
        string? inputName,
        string? inputCategory,
        IReadOnlyList<string>? inputAliases,
        string? inputStatus,
        string? currentNormalizedName,
        CancellationToken cancellationToken)
    {
        var name = inputName?.Trim() ?? string.Empty;
        var category = inputCategory?.Trim() ?? string.Empty;
        var status = string.IsNullOrWhiteSpace(inputStatus) ? "Active" : inputStatus.Trim();
        var aliases = NormalizeAliases(inputAliases);

        if (name.Length < 2 && !string.Equals(name, "R", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("admin_skills.name_invalid", "Skill name must be at least 2 characters unless it is a recognized one-letter skill such as R.");
        }

        if (name.Length > 160)
        {
            return Result.Failure("admin_skills.name_too_long", "Skill name cannot exceed 160 characters.");
        }

        if (category.Length < 2)
        {
            return Result.Failure("admin_skills.category_invalid", "Skill category must be at least 2 characters.");
        }

        if (category.Length > 100)
        {
            return Result.Failure("admin_skills.category_too_long", "Skill category cannot exceed 100 characters.");
        }

        if (!ValidStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure("admin_skills.status_invalid", "Skill status must be Active or Inactive.");
        }

        if (aliases.Any(alias => alias.Length > 160))
        {
            return Result.Failure("admin_skills.alias_too_long", "Skill aliases cannot exceed 160 characters.");
        }

        var normalizedName = NormalizeSkillName(name);
        if (!string.Equals(normalizedName, currentNormalizedName, StringComparison.OrdinalIgnoreCase) &&
            await _repository.SkillNormalizedNameExistsAsync(
                    _currentUser.TenantId,
                    normalizedName,
                    cancellationToken))
        {
            return Result.Failure("admin_skills.duplicate", "A skill with this name already exists.");
        }

        return Result.Success();
    }

    private static string NormalizeSkillName(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string[] NormalizeAliases(IReadOnlyList<string>? aliases)
    {
        return (aliases ?? [])
            .Select(alias => alias.Trim())
            .Where(alias => alias.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
