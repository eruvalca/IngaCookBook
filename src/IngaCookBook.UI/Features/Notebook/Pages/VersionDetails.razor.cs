using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class VersionDetails
{
    [Parameter] public Guid RecipeId { get; set; }
    [Parameter] public Guid VersionId { get; set; }
    private RecipeDocument? _recipe;
    private RecipeVersion? _version;
    private string _currency = "USD";
    private IngredientCost _cost = new(0, 0);

    protected override Task OnParametersSetAsync() => RunAsync(async () =>
    {
        _recipe = await Notebook.GetRecipeAsync(RecipeId);
        _version = _recipe?.Versions.FirstOrDefault(v => v.Id == VersionId);
        _currency = (await Notebook.GetWorkspaceAsync())?.Currency ?? "USD";
        if (_version is not null)
        {
            _cost = RecipeCosting.Calculate(_version.Content);
        }
    });

    private string PhotoUrl(Guid photoId) => $"/api/notebook/recipes/{RecipeId}/versions/{VersionId}/photos/{photoId}";
}
