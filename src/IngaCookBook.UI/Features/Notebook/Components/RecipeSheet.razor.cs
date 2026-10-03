using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class RecipeSheet
{
    [Parameter, EditorRequired] public RecipeContent Content { get; set; } = default!;
    [Parameter] public bool ExpandNested { get; set; }
}
