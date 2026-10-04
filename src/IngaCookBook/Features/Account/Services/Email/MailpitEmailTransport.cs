using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace IngaCookBook.Features.Account.Services.Email;

internal sealed class MailpitEmailTransport(IOptions<AccountEmailOptions> options) : IAccountEmailTransport
{
    public async Task SendAsync(AccountEmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var endpoint = new Uri(settings.MailpitEndpoint);
        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Inga’s recipe notebook", settings.SenderAddress));
        mime.To.Add(MailboxAddress.Parse(message.Recipient));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.Html, TextBody = message.Text }.ToMessageBody();
        using var client = new SmtpClient();
        // Development-only capture service. No TLS, authentication, forwarding, or relay.
        await client.ConnectAsync(endpoint.Host, endpoint.Port, SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        // Delivery is acknowledged; disposing the client closes the local connection.
    }
}
