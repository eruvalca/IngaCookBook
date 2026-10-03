using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class LocalDate
{
    [Parameter, EditorRequired] public DateTimeOffset Value { get; set; }
}
