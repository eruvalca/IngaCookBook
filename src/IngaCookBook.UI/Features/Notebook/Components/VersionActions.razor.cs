using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class VersionActions
{
    [Parameter, EditorRequired] public Guid RecipeId { get; set; }
    [Parameter, EditorRequired] public Guid VersionId { get; set; }
    [Parameter, EditorRequired] public Guid Revision { get; set; }
    [Parameter] public bool Locked { get; set; }
    [Parameter] public bool IsStandard { get; set; }
    [Parameter, EditorRequired] public bool OnlyVersion { get; set; }
    private string _newName = "";

    private Task DeleteDraftAsync() => RunAsync(async ct =>
    {
        if (Saved(await ReceiveAsync(Notebook.DeleteDraftAsync(RecipeId, VersionId, new(Revision), ct), ct)))
        {
            Navigation.NavigateTo(OnlyVersion ? "/recipes" : $"/recipes/{RecipeId}");
        }
    });

    private Task VaryAsync() => RunAsync(async ct =>
    {
        var result = await ReceiveAsync(Notebook.VaryAsync(RecipeId, VersionId, new(Revision), ct), ct);
        if (Saved(result) && result is ChangeSaved saved)
        {
            Navigation.NavigateTo($"/recipes/{RecipeId}/versions/{saved.Id}/edit");
        }
    });

    private Task StandardAsync() => RunAsync(async ct =>
    {
        if (Saved(await ReceiveAsync(Notebook.SetStandardAsync(RecipeId, VersionId, new(Revision), ct), ct)))
        {
            Navigation.Refresh();
        }
    });

    private Task PromoteAsync() => RunAsync(async ct =>
    {
        var result = await ReceiveAsync(Notebook.PromoteAsync(RecipeId, VersionId, new(Revision, _newName), ct), ct);
        if (Saved(result) && result is ChangeSaved saved)
        {
            Navigation.NavigateTo($"/recipes/{saved.Id}");
        }
    });
}
