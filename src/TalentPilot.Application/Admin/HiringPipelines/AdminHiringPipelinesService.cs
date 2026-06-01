using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;
using System.Text.Json;

namespace TalentPilot.Application.Admin.HiringPipelines;

public sealed class AdminHiringPipelinesService : IAdminHiringPipelinesService
{
    private static readonly string[] ValidStatuses = ["Active", "Inactive"];

    private readonly IAdminHiringPipelinesRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminHiringPipelinesService(
        IAdminHiringPipelinesRepository repository,
        ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminHiringPipelineTemplatesResponse>> ListTemplatesAsync(
        AdminHiringPipelineTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100)
        };

        var response = await _repository.ListTemplatesAsync(_currentUser.TenantId, normalized, cancellationToken);
        return Result<AdminHiringPipelineTemplatesResponse>.Success(response);
    }

    public async Task<Result<AdminHiringPipelineTemplateDetails>> GetTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var template = await _repository.GetHiringPipelineTemplateAsync(_currentUser.TenantId, templateId, cancellationToken);
        return template is null
            ? Result<AdminHiringPipelineTemplateDetails>.Failure(
                "admin_hiring_pipeline.not_found",
                "Interview template was not found.")
            : Result<AdminHiringPipelineTemplateDetails>.Success(template);
    }

    public async Task<Result<AdminHiringPipelineTemplateDetails>> UpdateTemplateAsync(
        Guid templateId,
        UpdateAdminHiringPipelineTemplateInput input,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetHiringPipelineTemplateAsync(_currentUser.TenantId, templateId, cancellationToken);
        if (existing is null)
        {
            return Result<AdminHiringPipelineTemplateDetails>.Failure(
                "admin_hiring_pipeline.not_found",
                "Interview template was not found.");
        }

        var validation = await ValidateUpdateInputAsync(templateId, input, cancellationToken);
        if (validation.Failed)
        {
            return Result<AdminHiringPipelineTemplateDetails>.Failure(validation.Error.Code, validation.Error.Message);
        }

        var normalizedRounds = input.Rounds
            .OrderBy(round => round.RoundOrder)
            .Select((round, index) => new UpdateAdminHiringPipelineTemplateRoundInput(
                round.InterviewTemplateRoundId,
                index + 1,
                round.Name.Trim(),
                round.OwnerRoleId,
                round.OwnerUserId,
                round.DurationMinutes,
                true,
                NormalizeStatus(round.Status)))
            .ToArray();
        var normalized = new UpdateAdminHiringPipelineTemplateInput(
            input.Name.Trim(),
            input.DepartmentId,
            string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            NormalizeStatus(input.Status),
            normalizedRounds);
        var metadataJson = JsonSerializer.Serialize(new
        {
            action = "update",
            templateId,
            previousName = existing.Name,
            normalized.Name,
            normalized.DepartmentId,
            roundCount = normalized.Rounds.Count,
            normalized.Status
        });

        await _repository.UpdateTemplateAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            templateId,
            normalized,
            metadataJson,
            cancellationToken);

        var template = await _repository.GetHiringPipelineTemplateAsync(_currentUser.TenantId, templateId, cancellationToken);
        return template is null
            ? Result<AdminHiringPipelineTemplateDetails>.Failure(
                "admin_hiring_pipeline.not_found",
                "Interview template was updated but could not be loaded.")
            : Result<AdminHiringPipelineTemplateDetails>.Success(template);
    }

    public async Task<Result<AdminHiringPipelineTemplateDetails>> CreateTemplateAsync(
        CreateAdminHiringPipelineTemplateInput input,
        CancellationToken cancellationToken)
    {
        var normalizedInput = ToUpdateInput(input);
        var validation = await ValidateUpdateInputAsync(Guid.Empty, normalizedInput, cancellationToken);
        if (validation.Failed)
        {
            return Result<AdminHiringPipelineTemplateDetails>.Failure(validation.Error.Code, validation.Error.Message);
        }

        var templateId = Guid.NewGuid();
        var normalizedRounds = normalizedInput.Rounds
            .OrderBy(round => round.RoundOrder)
            .Select((round, index) => new UpdateAdminHiringPipelineTemplateRoundInput(
                null,
                index + 1,
                round.Name.Trim(),
                round.OwnerRoleId,
                round.OwnerUserId,
                round.DurationMinutes,
                true,
                NormalizeStatus(round.Status)))
            .ToArray();
        var normalized = new UpdateAdminHiringPipelineTemplateInput(
            normalizedInput.Name.Trim(),
            normalizedInput.DepartmentId,
            string.IsNullOrWhiteSpace(normalizedInput.Description) ? null : normalizedInput.Description.Trim(),
            NormalizeStatus(normalizedInput.Status),
            normalizedRounds);
        var metadataJson = JsonSerializer.Serialize(new
        {
            action = "create",
            templateId,
            normalized.Name,
            normalized.DepartmentId,
            roundCount = normalized.Rounds.Count,
            normalized.Status
        });

        await _repository.CreateTemplateAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            templateId,
            normalized,
            metadataJson,
            cancellationToken);

        var template = await _repository.GetHiringPipelineTemplateAsync(_currentUser.TenantId, templateId, cancellationToken);
        return template is null
            ? Result<AdminHiringPipelineTemplateDetails>.Failure(
                "admin_hiring_pipeline.not_found",
                "Interview template was created but could not be loaded.")
            : Result<AdminHiringPipelineTemplateDetails>.Success(template);
    }

    private async Task<Result> ValidateUpdateInputAsync(
        Guid templateId,
        UpdateAdminHiringPipelineTemplateInput input,
        CancellationToken cancellationToken)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length < 2)
        {
            return Result.Failure("admin_hiring_pipeline.name_invalid", "Template name must be at least 2 characters.");
        }

        if (name.Length > 200)
        {
            return Result.Failure("admin_hiring_pipeline.name_too_long", "Template name cannot exceed 200 characters.");
        }

        if (!string.IsNullOrWhiteSpace(input.Description) && input.Description.Trim().Length > 500)
        {
            return Result.Failure("admin_hiring_pipeline.description_too_long", "Description cannot exceed 500 characters.");
        }

        if (!ValidStatuses.Contains(NormalizeStatus(input.Status), StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure("admin_hiring_pipeline.status_invalid", "Template status must be Active or Inactive.");
        }

        if (await _repository.TemplateNameExistsAsync(_currentUser.TenantId, name, templateId, cancellationToken))
        {
            return Result.Failure("admin_hiring_pipeline.duplicate", "A template with this name already exists.");
        }

        if (input.DepartmentId.HasValue &&
            !await _repository.DepartmentExistsAsync(_currentUser.TenantId, input.DepartmentId.Value, cancellationToken))
        {
            return Result.Failure("admin_hiring_pipeline.department_invalid", "Selected department was not found.");
        }

        if (input.Rounds is not { Count: > 0 })
        {
            return Result.Failure("admin_hiring_pipeline.rounds_required", "At least one interview round is required.");
        }

        if (input.Rounds.Count > 12)
        {
            return Result.Failure("admin_hiring_pipeline.too_many_rounds", "A template cannot have more than 12 rounds.");
        }

        if (!input.Rounds.Any(round => NormalizeStatus(round.Status) == "Active"))
        {
            return Result.Failure("admin_hiring_pipeline.active_round_required", "At least one active interview round is required.");
        }

        var roleIds = input.Rounds
            .Select(round => round.OwnerRoleId)
            .Where(roleId => roleId.HasValue && roleId.Value != Guid.Empty)
            .Select(roleId => roleId!.Value)
            .Distinct()
            .ToArray();
        if (roleIds.Length > 0 &&
            !await _repository.RoleIdsExistAsync(_currentUser.TenantId, roleIds, cancellationToken))
        {
            return Result.Failure("admin_hiring_pipeline.roles_invalid", "All selected legacy owner roles must exist.");
        }

        var userIds = input.Rounds
            .Select(round => round.OwnerUserId)
            .Where(userId => userId.HasValue && userId.Value != Guid.Empty)
            .Select(userId => userId!.Value)
            .Distinct()
            .ToArray();
        if (userIds.Length > 0 &&
            !await _repository.ActiveUserIdsExistAsync(_currentUser.TenantId, userIds, cancellationToken))
        {
            return Result.Failure("admin_hiring_pipeline.interviewers_invalid", "All selected interviewers must be active tenant users.");
        }

        foreach (var round in input.Rounds)
        {
            var roundName = round.Name?.Trim() ?? string.Empty;
            if (roundName.Length < 2)
            {
                return Result.Failure("admin_hiring_pipeline.round_name_invalid", "Round name must be at least 2 characters.");
            }

            if (roundName.Length > 160)
            {
                return Result.Failure("admin_hiring_pipeline.round_name_too_long", "Round name cannot exceed 160 characters.");
            }

            if (round.DurationMinutes is < 15 or > 480)
            {
                return Result.Failure("admin_hiring_pipeline.duration_invalid", "Round duration must be between 15 and 480 minutes.");
            }

            if (!ValidStatuses.Contains(NormalizeStatus(round.Status), StringComparer.OrdinalIgnoreCase))
            {
                return Result.Failure("admin_hiring_pipeline.round_status_invalid", "Round status must be Active or Inactive.");
            }
        }

        return Result.Success();
    }

    private static string NormalizeStatus(string? status)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "Active" : status.Trim();
        return ValidStatuses.FirstOrDefault(valid => valid.Equals(normalized, StringComparison.OrdinalIgnoreCase)) ?? normalized;
    }

    private static UpdateAdminHiringPipelineTemplateInput ToUpdateInput(CreateAdminHiringPipelineTemplateInput input)
    {
        return new UpdateAdminHiringPipelineTemplateInput(
            input.Name,
            input.DepartmentId,
            input.Description,
            input.Status,
            (input.Rounds ?? Array.Empty<CreateAdminHiringPipelineTemplateRoundInput>())
                .Select(round => new UpdateAdminHiringPipelineTemplateRoundInput(
                    null,
                    round.RoundOrder,
                    round.Name,
                    round.OwnerRoleId,
                    round.OwnerUserId,
                    round.DurationMinutes,
                    true,
                    round.Status))
                .ToArray());
    }
}
