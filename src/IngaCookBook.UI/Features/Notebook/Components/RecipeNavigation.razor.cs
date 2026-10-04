using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class RecipeNavigation
{
    [Parameter, EditorRequired] public Guid RecipeId { get; set; }
    [Parameter, EditorRequired] public Guid VersionId { get; set; }
    [Parameter, EditorRequired] public string Section { get; set; } = "";
    private string BaseUrl => $"/recipes/{RecipeId}/versions/{VersionId}";
}
