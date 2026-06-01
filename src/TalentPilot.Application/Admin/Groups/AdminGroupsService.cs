using System.Text.Json;
using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Groups;

public sealed class AdminGroupsService : IAdminGroupsService
{
    private static readonly string[] ValidMembershipFilters = ["All", "Members", "Available"];
    private static readonly string[] ValidBulkModes = ["AddMatching", "RemoveMatching"];
    private static readonly string[] ValidStatuses = ["Active", "Inactive"];

    private readonly IAdminGroupsRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminGroupsService(IAdminGroupsRepository repository, ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminGroupsResponse>> ListAsync(AdminGroupsQuery query, CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 100)
        };

        var response = await _repository.ListAsync(_currentUser.TenantId, normalized, cancellationToken);
        return Result<AdminGroupsResponse>.Success(response);
    }

    public async Task<Result<AdminGroupListItem>> CreateAsync(CreateGroupInput input, CancellationToken cancellationToken)
    {
        var validation = await ValidateCreateInputAsync(input, cancellationToken);
        if (validation.Failed)
        {
            return Result<AdminGroupListItem>.Failure(validation.Error.Code, validation.Error.Message);
        }

        var normalized = new CreateGroupInput(
            input.Name.Trim(),
            input.Purpose.Trim(),
            string.IsNullOrWhiteSpace(input.Status) ? "Active" : input.Status.Trim());
        var metadataJson = JsonSerializer.Serialize(new
        {
            action = "create",
            normalized.Name,
            normalized.Purpose,
            normalized.Status
        });

        var groupId = await _repository.CreateAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            normalized,
            metadataJson,
            cancellationToken);

        var group = await _repository.GetGroupAsync(_currentUser.TenantId, groupId, cancellationToken);
        return group is null
            ? Result<AdminGroupListItem>.Failure("admin_groups.not_found", "Group was created but could not be loaded.")
            : Result<AdminGroupListItem>.Success(group);
    }

    public async Task<Result<AdminGroupMembershipResponse>> ListMembershipAsync(
        Guid groupId,
        AdminGroupMembershipQuery query,
        CancellationToken cancellationToken)
    {
        var group = await _repository.GetGroupAsync(_currentUser.TenantId, groupId, cancellationToken);
        if (group is null)
        {
            return Result<AdminGroupMembershipResponse>.Failure("admin_groups.not_found", "Group was not found.");
        }

        var membershipResult = NormalizeMembershipFilter(query.Membership);
        if (membershipResult.Failed)
        {
            return Result<AdminGroupMembershipResponse>.Failure(
                membershipResult.Error.Code,
                membershipResult.Error.Message);
        }

        var normalized = query with
        {
            Membership = membershipResult.Value,
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 10 : query.PageSize, 1, 100)
        };

        return Result<AdminGroupMembershipResponse>.Success(
            await _repository.ListMembershipAsync(_currentUser.TenantId, groupId, normalized, cancellationToken));
    }

    public async Task<Result<UpdateGroupMembersResult>> UpdateMembershipAsync(
        Guid groupId,
        UpdateGroupMembersInput input,
        CancellationToken cancellationToken)
    {
        var group = await _repository.GetGroupAsync(_currentUser.TenantId, groupId, cancellationToken);
        if (group is null)
        {
            return Result<UpdateGroupMembersResult>.Failure("admin_groups.not_found", "Group was not found.");
        }

        if (!string.Equals(group.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Result<UpdateGroupMembersResult>.Failure(
                "admin_groups.inactive",
                "Inactive groups cannot be changed.");
        }

        var userIdsToAdd = (input.UserIdsToAdd ?? [])
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();
        var userIdsToRemove = (input.UserIdsToRemove ?? [])
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();
        var bulkSelection = NormalizeBulkSelection(input.BulkSelection);
        if (bulkSelection.Failed)
        {
            return Result<UpdateGroupMembersResult>.Failure(bulkSelection.Error.Code, bulkSelection.Error.Message);
        }

        if (userIdsToAdd.Intersect(userIdsToRemove).Any())
        {
            return Result<UpdateGroupMembersResult>.Failure(
                "admin_groups.membership_conflict",
                "The same user cannot be added and removed in one request.");
        }

        var affectedUserIds = userIdsToAdd.Concat(userIdsToRemove).Distinct().ToArray();
        if (affectedUserIds.Length == 0 && bulkSelection.Value is null)
        {
            return Result<UpdateGroupMembersResult>.Success(new UpdateGroupMembersResult(0, 0, group.MemberCount));
        }

        if (!await _repository.InternalUsersExistAsync(_currentUser.TenantId, affectedUserIds, cancellationToken))
        {
            return Result<UpdateGroupMembersResult>.Failure(
                "admin_groups.users_invalid",
                "All selected users must be internal users in this tenant.");
        }

        var normalized = new UpdateGroupMembersInput(userIdsToAdd, userIdsToRemove, bulkSelection.Value);
        var metadataJson = JsonSerializer.Serialize(new
        {
            action = "bulk-membership-update",
            groupId,
            userIdsToAdd,
            userIdsToRemove,
            bulkSelection = bulkSelection.Value
        });

        return Result<UpdateGroupMembersResult>.Success(await _repository.UpdateMembershipAsync(
            _currentUser.TenantId,
            _currentUser.UserId,
            groupId,
            normalized,
            metadataJson,
            cancellationToken));
    }

    private async Task<Result> ValidateCreateInputAsync(CreateGroupInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length < 2)
        {
            return Result.Failure("admin_groups.name_invalid", "Group name must be at least 2 characters.");
        }

        if (input.Name.Trim().Length > 160)
        {
            return Result.Failure("admin_groups.name_too_long", "Group name cannot exceed 160 characters.");
        }

        if (string.IsNullOrWhiteSpace(input.Purpose))
        {
            return Result.Failure("admin_groups.purpose_invalid", "Group purpose is required.");
        }

        if (input.Purpose.Trim().Length > 80)
        {
            return Result.Failure("admin_groups.purpose_too_long", "Group purpose cannot exceed 80 characters.");
        }

        var status = string.IsNullOrWhiteSpace(input.Status) ? "Active" : input.Status.Trim();
        if (!ValidStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure("admin_groups.status_invalid", "Group status must be Active or Inactive.");
        }

        if (await _repository.GroupNameExistsAsync(
                _currentUser.TenantId,
                input.Purpose.Trim(),
                input.Name.Trim(),
                cancellationToken))
        {
            return Result.Failure("admin_groups.duplicate", "A group with this name and purpose already exists.");
        }

        return Result.Success();
    }

    private static Result<string> NormalizeMembershipFilter(string? membership)
    {
        var normalized = string.IsNullOrWhiteSpace(membership) ? "All" : membership.Trim();
        return ValidMembershipFilters.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? Result<string>.Success(ValidMembershipFilters.First(filter => filter.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            : Result<string>.Failure(
                "admin_groups.membership_filter_invalid",
                "Membership filter must be All, Members, or Available.");
    }

    private static Result<BulkGroupMembershipSelection?> NormalizeBulkSelection(BulkGroupMembershipSelection? selection)
    {
        if (selection is null)
        {
            return Result<BulkGroupMembershipSelection?>.Success(null);
        }

        var mode = selection.Mode.Trim();
        if (!ValidBulkModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return Result<BulkGroupMembershipSelection?>.Failure(
                "admin_groups.bulk_mode_invalid",
                "Bulk membership mode must be AddMatching or RemoveMatching.");
        }

        var membership = NormalizeMembershipFilter(selection.Membership);
        if (membership.Failed)
        {
            return Result<BulkGroupMembershipSelection?>.Failure(membership.Error.Code, membership.Error.Message);
        }

        return Result<BulkGroupMembershipSelection?>.Success(new BulkGroupMembershipSelection(
            ValidBulkModes.First(validMode => validMode.Equals(mode, StringComparison.OrdinalIgnoreCase)),
            string.IsNullOrWhiteSpace(selection.Search) ? null : selection.Search.Trim(),
            membership.Value));
    }
}
