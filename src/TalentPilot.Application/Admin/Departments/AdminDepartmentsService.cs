using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;
using System.Text.Json;

namespace TalentPilot.Application.Admin.Departments;

public sealed class AdminDepartmentsService : IAdminDepartmentsService
{
    private static readonly string[] ValidStatuses = ["Active", "Inactive"];

    private readonly IAdminDepartmentsRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminDepartmentsService(IAdminDepartmentsRepository repository, ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminDepartmentsResponse>> ListAsync(
        AdminDepartmentsQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100)
        };

        var response = await _repository.ListAsync(_currentUser.TenantId, normalized, cancellationToken);
        return Result<AdminDepartmentsResponse>.Success(response);
    }

    public async Task<Result<AdminDepartmentListItem>> CreateAsync(
        CreateDepartmentInput input,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateCreateInputAsync(input, cancellationToken);
        if (validation.Failed)
        {
            return Result<AdminDepartmentListItem>.Failure(validation.Error.Code, validation.Error.Message);
        }

        var normalized = new CreateDepartmentInput(
            input.Code.Trim().ToUpperInvariant(),
            input.Name.Trim(),
            string.IsNullOrWhiteSpace(input.Status) ? "Active" : input.Status.Trim());
        var metadataJson = JsonSerializer.Serialize(new
        {
            action = "create",
            normalized.Code,
            normalized.Name,
            normalized.Status
        });

        var departmentId = await _repository.CreateAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            normalized,
            metadataJson,
            cancellationToken);

        var department = await _repository.GetDepartmentAsync(_currentUser.TenantId, departmentId, cancellationToken);
        return department is null
            ? Result<AdminDepartmentListItem>.Failure("admin_departments.not_found", "Department was created but could not be loaded.")
            : Result<AdminDepartmentListItem>.Success(department);
    }

    private async Task<Result> ValidateCreateInputAsync(CreateDepartmentInput input, CancellationToken cancellationToken)
    {
        var code = input.Code?.Trim() ?? string.Empty;
        var name = input.Name?.Trim() ?? string.Empty;
        var status = string.IsNullOrWhiteSpace(input.Status) ? "Active" : input.Status.Trim();

        if (code.Length < 2)
        {
            return Result.Failure("admin_departments.code_invalid", "Department code must be at least 2 characters.");
        }

        if (code.Length > 80)
        {
            return Result.Failure("admin_departments.code_too_long", "Department code cannot exceed 80 characters.");
        }

        if (!code.All(character => char.IsLetterOrDigit(character) || character is '-' or '_'))
        {
            return Result.Failure("admin_departments.code_invalid", "Department code can only contain letters, numbers, hyphens, or underscores.");
        }

        if (name.Length < 2)
        {
            return Result.Failure("admin_departments.name_invalid", "Department name must be at least 2 characters.");
        }

        if (name.Length > 160)
        {
            return Result.Failure("admin_departments.name_too_long", "Department name cannot exceed 160 characters.");
        }

        if (!ValidStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure("admin_departments.status_invalid", "Department status must be Active or Inactive.");
        }

        if (await _repository.DepartmentCodeOrNameExistsAsync(
                _currentUser.TenantId,
                code,
                name,
                cancellationToken))
        {
            return Result.Failure("admin_departments.duplicate", "A department with this code or name already exists.");
        }

        return Result.Success();
    }
}
