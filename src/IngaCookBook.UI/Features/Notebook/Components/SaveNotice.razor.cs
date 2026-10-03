using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class SaveNotice
{
    [Parameter] public string? Error { get; set; }
    [Parameter] public string? Status { get; set; }
}
