namespace IngaCookBook.Features.Account.Services.Email;

internal interface IAccountEmailTransport
{
    Task SendAsync(AccountEmailMessage message, CancellationToken cancellationToken);
}
