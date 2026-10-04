namespace IngaCookBook.Features.Account.Services.Email;

internal sealed class DisabledEmailTransport : IAccountEmailTransport
{
    public Task SendAsync(AccountEmailMessage message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Account email is disabled. Use owner-assisted account recovery.");
}
