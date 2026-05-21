using System.Text.Json;
using System.Text.RegularExpressions;
using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.Notifications;

public sealed class AdminNotificationsService : IAdminNotificationsService
{
    private static readonly Regex TemplateVariablePattern = new("{{\\s*([a-zA-Z][a-zA-Z0-9_]*)\\s*}}", RegexOptions.Compiled);
    private static readonly string[] ValidStatuses = ["Active", "Inactive"];

    private readonly IAdminNotificationsRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;

    public AdminNotificationsService(IAdminNotificationsRepository repository, ICurrentUserAccessor currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminNotificationEventsResponse>> ListEventsAsync(
        AdminNotificationEventsQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100)
        };

        var response = await _repository.ListEventsAsync(_currentUser.TenantId, normalized, cancellationToken);
        return Result<AdminNotificationEventsResponse>.Success(response);
    }

    public async Task<Result<AdminNotificationEventDetails>> GetEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var item = await _repository.GetEventAsync(_currentUser.TenantId, eventId, cancellationToken);
        return item is null
            ? Result<AdminNotificationEventDetails>.Failure("notifications.event_not_found", "Notification event was not found.")
            : Result<AdminNotificationEventDetails>.Success(item);
    }

    public async Task<Result<IReadOnlyList<NotificationTemplateSummary>>> ListTemplatesAsync(CancellationToken cancellationToken)
    {
        var templates = await _repository.ListTemplatesAsync(_currentUser.TenantId, cancellationToken);
        return Result<IReadOnlyList<NotificationTemplateSummary>>.Success(templates);
    }

    public async Task<Result<NotificationTemplateSummary>> UpdateTemplateAsync(
        Guid templateId,
        UpdateNotificationTemplateInput input,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Subject) || string.IsNullOrWhiteSpace(input.Body))
        {
            return Result<NotificationTemplateSummary>.Failure("notifications.template_invalid", "Template subject and body are required.");
        }

        var existing = await _repository.GetTemplateAsync(_currentUser.TenantId, templateId, cancellationToken);
        if (existing is null)
        {
            return Result<NotificationTemplateSummary>.Failure("notifications.template_not_found", "Notification template was not found.");
        }

        var allowedVariables = existing.Variables.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedVariables = ExtractVariables(input.Subject)
            .Concat(ExtractVariables(input.Body))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var invalidVariable = usedVariables.FirstOrDefault(variable => !allowedVariables.Contains(variable));
        if (invalidVariable is not null)
        {
            return Result<NotificationTemplateSummary>.Failure("notifications.variable_invalid", $"Template variable '{invalidVariable}' is not allowed for this event.");
        }

        var metadataJson = JsonSerializer.Serialize(new { action = "template_update", templateId, usedVariables });
        await _repository.UpdateTemplateAsync(_currentUser.TenantId, _currentUser.UserId, templateId, input, metadataJson, cancellationToken);

        var updated = await _repository.GetTemplateAsync(_currentUser.TenantId, templateId, cancellationToken);
        return Result<NotificationTemplateSummary>.Success(updated!);
    }

    public async Task<Result> UpdateEventStatusAsync(
        Guid eventId,
        UpdateNotificationEventStatusInput input,
        CancellationToken cancellationToken)
    {
        if (!ValidStatuses.Contains(input.Status, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure("notifications.status_invalid", "Notification event status must be Active or Inactive.");
        }

        var existing = await _repository.GetEventAsync(_currentUser.TenantId, eventId, cancellationToken);
        if (existing is null)
        {
            return Result.Failure("notifications.event_not_found", "Notification event was not found.");
        }

        var metadataJson = JsonSerializer.Serialize(new { action = "event_status", eventId, input.Status });
        await _repository.UpdateEventStatusAsync(_currentUser.TenantId, _currentUser.UserId, eventId, input.Status, metadataJson, cancellationToken);
        return Result.Success();
    }

    private static IEnumerable<string> ExtractVariables(string template)
    {
        return TemplateVariablePattern
            .Matches(template)
            .Select(match => match.Groups[1].Value);
    }
}
