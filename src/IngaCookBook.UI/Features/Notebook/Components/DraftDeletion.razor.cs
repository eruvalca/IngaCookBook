using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class DraftDeletion
{
    [Parameter] public bool Disabled { get; set; }
    [Parameter, EditorRequired] public bool OnlyVersion { get; set; }
    [Parameter, EditorRequired] public EventCallback Confirmed { get; set; }
    private bool _confirming;

    private void ShowConfirmation() => _confirming = true;
    private void Cancel() => _confirming = false;
    private Task ConfirmAsync() => Disabled ? Task.CompletedTask : Confirmed.InvokeAsync();
}
