using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class Library
{
    [SupplyParameterFromQuery(Name = "q")] public string? Query { get; set; }
    private WorkspaceView? _workspace;
    private IReadOnlyList<RecipeDocument> _recipes = [];
    private bool _loaded;
    private IEnumerable<RecipeDocument> Filtered => _recipes.Where(r => string.IsNullOrWhiteSpace(Query) ||
        r.Name.Contains(Query, StringComparison.OrdinalIgnoreCase) || r.Description.Contains(Query, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnParametersSetAsync()
    {
        await RunAsync(async () =>
        {
            _workspace = await Notebook.GetWorkspaceAsync();
            _recipes = await Notebook.GetRecipesAsync();
            _loaded = true;
        });
    }

    private static string? Cover(RecipeDocument recipe)
    {
        var version = recipe.Versions.FirstOrDefault(v => v.Id == recipe.StandardVersionId)
            ?? recipe.Versions.FirstOrDefault(v => v.Photos.Count > 0);
        var photo = version?.Photos.Count > 0 ? version.Photos[0] : null;
        return photo is null ? null : $"/api/notebook/recipes/{recipe.Id}/versions/{version!.Id}/photos/{photo.Id}";
    }
}
