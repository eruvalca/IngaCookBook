using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;

namespace IngaCookBook.Features.Account.Services.Email;

internal sealed class AzureEmailTransport(EmailClient client, IOptions<AccountEmailOptions> options) : IAccountEmailTransport
{
    public async Task SendAsync(AccountEmailMessage message, CancellationToken cancellationToken)
    {
        var content = new EmailContent(message.Subject) { Html = message.Html, PlainText = message.Text };
        var operation = await client.SendAsync(WaitUntil.Completed,
            new EmailMessage(options.Value.SenderAddress, message.Recipient, content), cancellationToken);
        if (operation.Value.Status != EmailSendStatus.Succeeded)
        {
            throw new InvalidOperationException("Azure did not accept the account email for delivery.");
        }
        // Succeeded means the sending operation completed, not that a recipient read it
        // or that their mail server delivered it to an inbox. Do not log message bodies/tokens.
    }
}
