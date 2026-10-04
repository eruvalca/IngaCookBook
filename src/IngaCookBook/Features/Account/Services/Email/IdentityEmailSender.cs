using System.Net;
using System.Text.Encodings.Web;
using IngaCookBook.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace IngaCookBook.Features.Account.Services.Email;

internal sealed class IdentityEmailSender(
    IAccountEmailTransport transport,
    IHttpContextAccessor contextAccessor,
    IHostApplicationLifetime lifetime,
    IOptions<AccountEmailOptions> options) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendLinkAsync(email, "Confirm your email", "Confirm email address", confirmationLink);

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendLinkAsync(email, "Reset your password", "Reset password", resetLink);

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(new(email, "Reset your password",
            $"<h1>Inga’s recipe notebook</h1><p>Your password reset code is: <strong>{HtmlEncoder.Default.Encode(resetCode)}</strong></p><p>If you did not request this, you can ignore this email.</p>",
            $"Inga’s recipe notebook\nYour password reset code is: {resetCode}\nIf you did not request this, you can ignore this email."));

    private Task SendLinkAsync(string email, string subject, string action, string encodedLink)
    {
        // Identity's SSR pages supply an HTML-encoded URL. Decode once for plain text,
        // then encode once for the HTML attribute so query separators remain intact.
        var link = WebUtility.HtmlDecode(encodedLink);
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            throw new ArgumentException("Account email requires an absolute HTTP or HTTPS link.", nameof(encodedLink));
        }
        return SendAsync(new(email, subject,
            $"<h1>Inga’s recipe notebook</h1><p><a href=\"{HtmlEncoder.Default.Encode(link)}\">{action}</a></p><p>If you did not request this, you can ignore this email.</p>",
            $"Inga’s recipe notebook\n{action}:\n{link}\n\nIf you did not request this, you can ignore this email."));
    }

    private async Task SendAsync(AccountEmailMessage message)
    {
        // Identity's interface has no token. These account operations run in SSR requests,
        // not interactive circuits. Bound delivery even when there is no active request.
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            contextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None, lifetime.ApplicationStopping);
        cancellation.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        await transport.SendAsync(message, cancellation.Token);
    }
}
