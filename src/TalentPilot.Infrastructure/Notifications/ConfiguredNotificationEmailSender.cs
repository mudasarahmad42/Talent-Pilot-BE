using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Common.Results;

namespace TalentPilot.Infrastructure.Notifications;

internal sealed class ConfiguredNotificationEmailSender : INotificationEmailSender
{
    private readonly INotificationEmailProviderSettingsResolver _settingsResolver;
    private readonly IReadOnlyDictionary<string, INotificationEmailProviderSender> _sendersByProvider;

    public ConfiguredNotificationEmailSender(
        INotificationEmailProviderSettingsResolver settingsResolver,
        IEnumerable<INotificationEmailProviderSender> senders)
    {
        _settingsResolver = settingsResolver;
        _sendersByProvider = senders.ToDictionary(
            sender => NotificationEmailProviders.Normalize(sender.Provider),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Result<NotificationEmailSendResult>> SendAsync(
        NotificationEmailMessage message,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.GetAsync(message.TenantId, cancellationToken);
        var provider = NotificationEmailProviders.Normalize(settings.Provider);
        if (!NotificationEmailProviders.IsSupported(provider))
        {
            return Result<NotificationEmailSendResult>.Failure(
                "notifications.email_provider_invalid",
                $"Email provider '{settings.Provider}' is not supported.");
        }

        if (!_sendersByProvider.TryGetValue(provider, out var sender))
        {
            return Result<NotificationEmailSendResult>.Failure(
                "notifications.email_provider_not_registered",
                $"Email provider '{provider}' is not registered.");
        }

        try
        {
            return await sender.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<NotificationEmailSendResult>.Failure(
                "notifications.email_provider_failed",
                ToProviderFailureMessage(provider, exception));
        }
    }

    private static string ToProviderFailureMessage(string provider, Exception exception)
    {
        var detail = exception.Message.Trim();
        return string.IsNullOrWhiteSpace(detail)
            ? $"Email provider '{provider}' failed before completing the request."
            : $"Email provider '{provider}' failed: {detail}";
    }
}
