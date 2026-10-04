namespace IngaCookBook.Features.Account.Services.Email;

internal sealed class AccountEmailOptions
{
    public string Provider { get; set; } = "None";
    public bool Enabled => !string.Equals(Provider, "None", StringComparison.Ordinal);
    public string SenderAddress { get; set; } = "";
    public string MailpitEndpoint { get; set; } = "";
    public string AzureConnectionString { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
}
