namespace IngaCookBook.Features.Account.Services.Email;

internal sealed record AccountEmailMessage(string Recipient, string Subject, string Html, string Text);
