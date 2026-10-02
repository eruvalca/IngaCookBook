using Microsoft.AspNetCore.Identity;
using IngaCookBook.Data;

namespace IngaCookBook.Features.Account.Components;

public sealed partial class ManageNavMenu
{
    private bool _hasExternalLogins;

    protected override async Task OnInitializedAsync()
    {
        _hasExternalLogins = (await SignInManager.GetExternalAuthenticationSchemesAsync()).Any();
    }
}
