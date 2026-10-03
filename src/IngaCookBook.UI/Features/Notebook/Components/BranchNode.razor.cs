using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class BranchNode
{
    [Parameter, EditorRequired] public RecipeDocument Recipe { get; set; } = default!;
    [Parameter, EditorRequired] public RecipeVersion Version { get; set; } = default!;
    private IReadOnlyList<RecipeVersion> Children { get; set; } = [];
    protected override void OnParametersSet() => Children = Recipe.Versions.Where(v => v.ParentId == Version.Id).ToArray();
}
