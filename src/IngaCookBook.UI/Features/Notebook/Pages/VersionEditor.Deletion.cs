using System.Text.Json;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class VersionEditor
{
    private Task DeleteDraftAsync() => RunAsync(async () =>
    {
        if (_recipe is null || !Saved(await Notebook.DeleteDraftAsync(RecipeId, VersionId, new(_recipe.Revision))))
        {
            return;
        }
        // Confirmation covers the pending edits too; clear both navigation guards
        // before leaving, without prompting again after the draft is already gone.
        _saved = JsonSerializer.Serialize(_draft);
        _correctionReason = "";
        _savedRevision++;
        if (_navigationInterop is not null)
        {
            await _navigationInterop.UpdateAsync(_editor, false, _savedRevision);
        }
        Navigation.NavigateTo(_recipe.Versions.Count == 1 ? "/recipes" : $"/recipes/{RecipeId}");
    });
}
