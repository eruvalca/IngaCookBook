using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class KitchenSheet
{
    [Parameter, EditorRequired] public RecipeContent Content { get; set; } = new();
    private readonly HashSet<Guid> _completed = [];
    private void Toggle(Guid id, bool value)
    {
        if (value) { _completed.Add(id); }
        else { _completed.Remove(id); }
    }
    private void Reset() => _completed.Clear();
}
