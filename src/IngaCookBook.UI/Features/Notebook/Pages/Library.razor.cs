using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class Library
{
    [SupplyParameterFromQuery(Name = "q")] public string? Query { get; set; }
    [SupplyParameterFromQuery(Name = "status")] public string? StatusFilter { get; set; }
    private WorkspaceView? _workspace;
    private IReadOnlyList<RecipeDocument> _recipes = [];
    private bool _loaded;
    private IEnumerable<RecipeDocument> Filtered => _recipes.Where(r => (string.IsNullOrWhiteSpace(Query) ||
        r.Name.Contains(Query, StringComparison.OrdinalIgnoreCase) || r.Description.Contains(Query, StringComparison.OrdinalIgnoreCase)) && MatchesStatus(r));
    private bool MatchesStatus(RecipeDocument recipe) => StatusFilter switch
    {
        "drafts" => recipe.Versions.Any(v => !v.IsLocked),
        "tasting" => AwaitingTasting(recipe) is not null,
        "standards" => recipe.StandardVersionId is not null,
        _ => true,
    };
    private static RecipeVersion? Draft(RecipeDocument recipe) => recipe.Versions.Where(v => !v.IsLocked).MaxBy(v => v.CreatedAt);
    private (RecipeDocument Recipe, RecipeVersion Version)? RecentDraft => _recipes
        .SelectMany(r => r.Versions.Where(v => !v.IsLocked).Select(v => (Recipe: r, Version: v)))
        .OrderByDescending(x => x.Version.CreatedAt).Select(x => ((RecipeDocument Recipe, RecipeVersion Version)?)x).FirstOrDefault();
    private static string Count(int count, string noun)
    {
        if (count == 1) { return $"1 {noun}"; }
        var suffix = string.Equals(noun, "batch", StringComparison.Ordinal) ? "es" : "s";
        return $"{count} {noun}{suffix}";
    }
    private static RecipeVersion? AwaitingTasting(RecipeDocument recipe) => recipe.Versions.Where(v => v.Batches.Any(b => b.Evaluations.Count == 0)).MaxBy(v => v.CreatedAt);

    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync(async ct =>
        {
            _workspace = await ReceiveAsync(Notebook.GetWorkspaceAsync(ct), ct);
            _recipes = await ReceiveAsync(Notebook.GetRecipesAsync(ct), ct);
            _loaded = true;
        });
    }

    private static string? Cover(RecipeDocument recipe)
    {
        var version = recipe.Versions.FirstOrDefault(v => v.Photos.Any(p => p.Id == recipe.CoverPhotoId))
            ?? recipe.Versions.FirstOrDefault(v => v.Id == recipe.StandardVersionId && v.Photos.Count > 0)
            ?? recipe.Versions.FirstOrDefault(v => v.Photos.Count > 0);
        var firstPhoto = version?.Photos.Count > 0 ? version.Photos[0] : null;
        var photo = version?.Photos.FirstOrDefault(p => p.Id == recipe.CoverPhotoId) ?? firstPhoto;
        return photo is null ? null : $"/api/notebook/recipes/{recipe.Id}/versions/{version!.Id}/photos/{photo.Id}";
    }
}
