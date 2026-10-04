using Microsoft.AspNetCore.Components;

namespace IngaCookBook.Features.Account.Pages;

public sealed partial class RegisterConfirmation
{
    private string? _statusMessage;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? Email { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (!EmailOptions.Value.Enabled)
        {
            return;
        }
        if (Email is null)
        {
            RedirectManager.RedirectTo("");
            return;
        }
        var user = await UserManager.FindByEmailAsync(Email);
        if (user is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            _statusMessage = "Error finding user for unspecified email";
        }

    }
}
