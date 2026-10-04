using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class ChangeSummary
{
    [Parameter, EditorRequired] public IReadOnlyList<RecipeDifference> Changes { get; set; } = [];
    [Parameter] public int Limit { get; set; } = int.MaxValue;

}
