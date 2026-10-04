using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class Kitchen
{
    [Parameter] public Guid RecipeId { get; set; }
    [Parameter] public Guid VersionId { get; set; }
    private RecipeDocument? _recipe;
    private RecipeVersion? _version;
    protected override Task OnParametersSetAsync() => RunAsync(async () =>
    {
        _recipe = await Notebook.GetRecipeAsync(RecipeId);
        _version = _recipe?.Versions.FirstOrDefault(v => v.Id == VersionId);
    });
}
