using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Common.Results;

namespace TalentPilot.Infrastructure.Notifications;

internal interface INotificationEmailProviderSender
{
    string Provider { get; }

    Task<Result<NotificationEmailSendResult>> SendAsync(NotificationEmailMessage message, CancellationToken cancellationToken);
}
