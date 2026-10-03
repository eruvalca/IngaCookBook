using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class Recipe
{
    [Parameter] public Guid RecipeId { get; set; }
    [SupplyParameterFromQuery(Name = "view")] public string? View { get; set; }
    private RecipeDocument? _recipe;
    private bool Branches => string.Equals(View, "branches", StringComparison.Ordinal);
    protected override Task OnParametersSetAsync() => RunAsync(async () => _recipe = await Notebook.GetRecipeAsync(RecipeId));
    private string VersionUrl(RecipeVersion version) => $"/recipes/{RecipeId}/versions/{version.Id}";
}
