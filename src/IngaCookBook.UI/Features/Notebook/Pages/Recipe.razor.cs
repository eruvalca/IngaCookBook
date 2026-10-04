using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class Recipe
{
    [Parameter] public Guid RecipeId { get; set; }
    [SupplyParameterFromQuery(Name = "view")] public string? View { get; set; }
    private RecipeDocument? _recipe;
    private bool Branches => string.Equals(View, "branches", StringComparison.Ordinal);
    protected override Task OnParametersSetAsync() => LoadAsync(async ct => _recipe = await ReceiveAsync(Notebook.GetRecipeAsync(RecipeId, ct), ct));
    private string VersionUrl(RecipeVersion version) => $"/recipes/{RecipeId}/versions/{version.Id}";
}
