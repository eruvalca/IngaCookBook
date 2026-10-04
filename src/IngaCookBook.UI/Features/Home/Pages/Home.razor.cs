using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace IngaCookBook.UI.Features.Home.Pages;

public sealed partial class Home
{
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        if (AuthenticationState is not null && (await AuthenticationState).User.Identity?.IsAuthenticated == true)
        {
            Navigation.NavigateTo("/recipes");
        }
    }
}
