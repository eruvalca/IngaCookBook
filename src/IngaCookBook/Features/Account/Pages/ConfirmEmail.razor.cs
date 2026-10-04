using IngaCookBook.Data;
using IngaCookBook.Features.Account.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace IngaCookBook.Features.Account.Pages;

public sealed partial class ConfirmEmail
{
    private string? _statusMessage;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? UserId { get; set; }

    [SupplyParameterFromQuery]
    private string? Code { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (!EmailOptions.Value.Enabled)
        {
            _statusMessage = "Email confirmation is not required. You can log in with your password or passkey.";
            return;
        }
        if (UserId is null || Code is null)
        {
            RedirectManager.RedirectTo("");
            return;
        }

        var user = await UserManager.FindByIdAsync(UserId);
        if (user is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            _statusMessage = $"Error loading user with ID {UserId}";
        }
        else
        {
            await TokenDecodeOutcome.Decode(Code).Match(
                async token =>
                {
                    var result = await UserManager.ConfirmEmailAsync(user, token.Value);
                    _statusMessage = result.Succeeded ? "Thank you for confirming your email." : "Error confirming your email.";
                },
                _ =>
                {
                    _statusMessage = "Error confirming your email.";
                    return Task.CompletedTask;
                });
        }
    }
}
