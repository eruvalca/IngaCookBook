using System.ComponentModel.DataAnnotations;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class Workspace
{
    private WorkspaceView? _workspace;
    [SupplyParameterFromForm] private WorkspaceInput Input { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync(async ct =>
    {
        Input ??= new();
        _workspace = await ReceiveAsync(Notebook.GetWorkspaceAsync(ct), ct);
    });

    private Task CreateAsync() => RunAsync(async ct =>
    {
        if (Saved(await ReceiveAsync(Notebook.CreateWorkspaceAsync(new(Input.Name, Input.Currency), ct), ct)))
        {
            Navigation.NavigateTo("/recipes");
            return;
        }
    });

    private sealed class WorkspaceInput
    {
        [Required, StringLength(120)] public string Name { get; set; } = "";
        [Required] public string Currency { get; set; } = "USD";
    }
}
