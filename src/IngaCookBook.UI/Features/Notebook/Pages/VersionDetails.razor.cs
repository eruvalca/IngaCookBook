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

    protected override Task OnParametersSetAsync() => LoadAsync(async ct =>
    {
        _recipe = await ReceiveAsync(Notebook.GetRecipeAsync(RecipeId, ct), ct);
        _version = _recipe?.Versions.FirstOrDefault(v => v.Id == VersionId);
        _currency = (await ReceiveAsync(Notebook.GetWorkspaceAsync(ct), ct))?.Currency ?? "USD";
        if (_version is not null)
        {
            _cost = RecipeCosting.Calculate(_version.Content);
        }
    });

    private string PhotoUrl(Guid photoId) => $"/api/notebook/recipes/{RecipeId}/versions/{VersionId}/photos/{photoId}";
}
