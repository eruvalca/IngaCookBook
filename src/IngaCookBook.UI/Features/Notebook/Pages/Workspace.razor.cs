using System.ComponentModel.DataAnnotations;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class Workspace
{
    private WorkspaceView? _workspace;
    [SupplyParameterFromForm] private WorkspaceInput Input { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        Input ??= new();
        _workspace = await Notebook.GetWorkspaceAsync();
    }

    private async Task CreateAsync()
    {
        if (Saved(await Notebook.CreateWorkspaceAsync(new(Input.Name, Input.Currency))))
        {
            Navigation.NavigateTo("/recipes");
            return;
        }
    }

    private sealed class WorkspaceInput
    {
        [Required, StringLength(120)] public string Name { get; set; } = "";
        [Required] public string Currency { get; set; } = "USD";
    }
}
