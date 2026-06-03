using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Common.Results;

namespace TalentPilot.Infrastructure.Notifications;

internal sealed class MicrosoftGraphNotificationEmailSender : INotificationEmailProviderSender
{
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];

    private readonly MicrosoftGraphEmailOptions _options;

    public MicrosoftGraphNotificationEmailSender(IOptions<MicrosoftGraphEmailOptions> options)
    {
        _options = options.Value;
    }

    public string Provider => NotificationEmailProviders.MicrosoftGraph;

    public async Task<Result<NotificationEmailSendResult>> SendAsync(
        NotificationEmailMessage message,
        CancellationToken cancellationToken)
    {
        var validation = ValidateOptions();
        if (validation.Failed)
        {
            return Result<NotificationEmailSendResult>.Failure(validation.Error.Code, validation.Error.Message);
        }

        var credential = new ClientSecretCredential(
            _options.TenantId.Trim(),
            _options.ClientId.Trim(),
            _options.ClientSecret.Trim(),
            new ClientSecretCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            });

        var graphClient = new GraphServiceClient(credential, GraphScopes);
        var requestBody = new SendMailPostRequestBody
        {
            Message = new Message
            {
                Subject = message.Subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = string.IsNullOrWhiteSpace(message.HtmlBody)
                        ? message.TextBody
                        : message.HtmlBody
                },
                ToRecipients =
                [
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = message.ToEmail.Trim()
                        }
                    }
                ]
            },
            SaveToSentItems = _options.SaveToSentItems
        };

        try
        {
            await graphClient.Users[_options.FromEmail.Trim()]
                .SendMail
                .PostAsync(requestBody, cancellationToken: cancellationToken);

            return Result<NotificationEmailSendResult>.Success(new NotificationEmailSendResult(
                NotificationEmailProviders.MicrosoftGraph,
                string.Empty,
                DateTimeOffset.UtcNow));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<NotificationEmailSendResult>.Failure(
                "notifications.microsoft_graph_send_failed",
                ToGraphFailureMessage(exception));
        }
    }

    private Result ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.TenantId))
        {
            return Result.Failure(
                "notifications.microsoft_graph_tenant_missing",
                "Microsoft Graph tenant id is not configured for this environment.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            return Result.Failure(
                "notifications.microsoft_graph_client_missing",
                "Microsoft Graph client id is not configured for this environment.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            return Result.Failure(
                "notifications.microsoft_graph_secret_missing",
                "Microsoft Graph client secret is not configured for this environment.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            return Result.Failure(
                "notifications.microsoft_graph_sender_missing",
                "Microsoft Graph sender mailbox is not configured for this environment.");
        }

        return Result.Success();
    }

    private static string ToGraphFailureMessage(Exception exception)
    {
        var detail = exception.Message.Trim();
        return string.IsNullOrWhiteSpace(detail)
            ? "Microsoft Graph rejected the email. Check Mail.Send application consent, mailbox access, sender address, and recipient."
            : $"Microsoft Graph rejected the email: {detail}";
    }
}
