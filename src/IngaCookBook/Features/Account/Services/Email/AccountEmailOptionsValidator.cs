using Azure.Communication.Email;
using Microsoft.Extensions.Options;
using MimeKit;

namespace IngaCookBook.Features.Account.Services.Email;

internal sealed class AccountEmailOptionsValidator(IHostEnvironment environment) : IValidateOptions<AccountEmailOptions>
{
    public ValidateOptionsResult Validate(string? name, AccountEmailOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }
        var errors = new List<string>();
        if (!MailboxAddress.TryParse(options.SenderAddress, out var sender) ||
            !string.Equals(sender.Address, options.SenderAddress, StringComparison.Ordinal) ||
            !sender.Address.Contains('@', StringComparison.Ordinal))
        {
            errors.Add("Email:SenderAddress must be a single email address without a display name.");
        }
        if (options.TimeoutSeconds is < 1 or > 120)
        {
            errors.Add("Email:TimeoutSeconds must be between 1 and 120 seconds.");
        }
        switch (options.Provider)
        {
            case "Mailpit":
                if (!environment.IsDevelopment())
                {
                    errors.Add("Mailpit is only allowed in Development. Select None or Azure for other environments.");
                }
                if (!Uri.TryCreate(options.MailpitEndpoint, UriKind.Absolute, out var endpoint) ||
                    !string.Equals(endpoint.Scheme, "smtp", StringComparison.Ordinal) || endpoint.Port is < 1 or > 65535 || string.IsNullOrEmpty(endpoint.Host))
                {
                    errors.Add("Email:MailpitEndpoint must be the Aspire SMTP endpoint (smtp://host:port).");
                }
                break;
            case "Azure":
                try
                {
                    // Construction validates connection-string syntax; it makes no network request.
                    _ = new EmailClient(options.AzureConnectionString);
                }
                catch (Exception error) when (error is ArgumentException or FormatException or InvalidOperationException)
                {
                    // Never include the supplied connection string in validation messages.
                    errors.Add("Configure a valid ConnectionStrings:communicationemail using user secrets or deployment secrets.");
                }
                break;
            default:
                errors.Add("Email:Provider must be None, Mailpit or Azure.");
                break;
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
