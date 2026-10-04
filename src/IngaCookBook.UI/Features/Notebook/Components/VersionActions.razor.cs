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

    private Task DeleteDraftAsync() => RunAsync(async () =>
    {
        if (Saved(await Notebook.DeleteDraftAsync(RecipeId, VersionId, new(Revision))))
        {
            Navigation.NavigateTo(OnlyVersion ? "/recipes" : $"/recipes/{RecipeId}");
        }
    });

    private Task VaryAsync() => RunAsync(async () =>
    {
        var result = await Notebook.VaryAsync(RecipeId, VersionId, new(Revision));
        if (Saved(result) && result is ChangeSaved saved)
        {
            Navigation.NavigateTo($"/recipes/{RecipeId}/versions/{saved.Id}/edit");
        }
    });

    private Task StandardAsync() => RunAsync(async () =>
    {
        if (Saved(await Notebook.SetStandardAsync(RecipeId, VersionId, new(Revision))))
        {
            Navigation.Refresh();
        }
    });

    private Task PromoteAsync() => RunAsync(async () =>
    {
        var result = await Notebook.PromoteAsync(RecipeId, VersionId, new(Revision, _newName));
        if (Saved(result) && result is ChangeSaved saved)
        {
            Navigation.NavigateTo($"/recipes/{saved.Id}");
        }
    });
}
