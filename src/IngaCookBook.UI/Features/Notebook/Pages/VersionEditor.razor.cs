using System.Text.Json;
using IngaCookBook.SharedKernel.Notebook;
using IngaCookBook.UI.Features.Notebook.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class VersionEditor : IAsyncDisposable
{
    private readonly QuantityInputCulture _quantityCulture = new();
    [Parameter] public Guid RecipeId { get; set; }
    [Parameter] public Guid VersionId { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    private RecipeDocument? _recipe;
    private RecipeVersion? _version;
    private RecipeVersion? _parent;
    private VersionDraft _draft = new();
    private string _saved = "";
    private string _correctionReason = "";
    private string _currency = "USD";
    private EditorNavigationInterop? _navigationInterop;
    private ElementReference _editor;
    private int _savedRevision;
    private bool GuardReady { get; set; }
    private bool EditorDisabled => Disabled || !GuardReady;
    private string SaveStatus => Busy ? "Saving…" : UnsavedStatus;
    private string UnsavedStatus => Dirty ? "Unsaved changes" : "All changes saved";
    private IReadOnlyList<LinkOption> _links = [];
    private IngredientCost Cost => RecipeCosting.Calculate(_draft.ToContent());
    private bool Dirty => _recipe is not null && (!string.Equals(_saved, JsonSerializer.Serialize(_draft), StringComparison.Ordinal)
        || !string.IsNullOrEmpty(_correctionReason));
    private RecipeContent? ComparisonBaseline => _version?.IsLocked == true ? _version.Content : _parent?.Content;
    private IReadOnlyList<RecipeDifference> Changes => ComparisonBaseline is { } baseline
        ? RecipeComparison.Compare(baseline, _draft.ToContent()) : [];
    private IEnumerable<MetricOption> MetricOptions => new[] { new MetricOption("", "Choose a focus (optional)") }
        .Concat(_recipe?.Metrics.Select(m => new MetricOption(m.Id.ToString(), m.Name)) ?? []);

    protected override Task OnParametersSetAsync() => RunAsync(async () =>
    {
        _recipe = await Notebook.GetRecipeAsync(RecipeId);
        _version = _recipe?.Versions.FirstOrDefault(v => v.Id == VersionId);
        _parent = _recipe?.Versions.FirstOrDefault(v => v.Id == _version?.ParentId);
        _currency = (await Notebook.GetWorkspaceAsync())?.Currency ?? "USD";
        if (_version is not null)
        {
            _draft = VersionDraft.From(_version.Content);
            _saved = JsonSerializer.Serialize(_draft);
        }
        var recipes = await Notebook.GetRecipesAsync();
        _links = [new("", "No linked recipe", null), .. recipes.Where(r => r.Id != RecipeId)
            .SelectMany(r => r.Versions.Where(v => v.IsLocked).Select(v =>
                new LinkOption($"{r.Id}/{v.Id}", $"{r.Name} · V{v.Number}: {v.Content.Label}",
                    v.Content with { Label = $"{r.Name} · V{v.Number}: {v.Content.Label}" })))];
    });

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _navigationInterop ??= new EditorNavigationInterop(JavaScript);
        await _navigationInterop.UpdateAsync(_editor, Dirty, _savedRevision);
        if (!GuardReady)
        {
            GuardReady = true;
            StateHasChanged();
        }
    }

    private Task SaveAsync() => RunAsync(async () =>
    {
        if (_recipe is null)
        {
            return;
        }
        if (Saved(await Notebook.SaveVersionAsync(RecipeId, VersionId, new(_recipe.Revision, _draft.ToContent(), _correctionReason))))
        {
            _recipe = await Notebook.GetRecipeAsync(RecipeId);
            _version = _recipe!.Versions.First(v => v.Id == VersionId);
            _draft = VersionDraft.From(_version.Content);
            _saved = JsonSerializer.Serialize(_draft);
            _correctionReason = "";
            _savedRevision++;
        }
    });

    private void AddIngredient() => _draft.Ingredients.Add(new());
    private void AddStep() => _draft.Steps.Add(new());
    private void MoveStep(int index, int direction)
    {
        var step = _draft.Steps[index];
        _draft.Steps.RemoveAt(index);
        _draft.Steps.Insert(index + direction, step);
    }

    private void SetLink(IngredientInput ingredient, string value)
    {
        ingredient.Link = value;
        ingredient.LinkedContent = _links.FirstOrDefault(l => string.Equals(l.Value, value, StringComparison.Ordinal))?.Content;
    }

    private async Task BeforeNavigateAsync(LocationChangingContext context)
    {
        _navigationInterop ??= new EditorNavigationInterop(JavaScript);
        if (!await _navigationInterop.ConfirmDiscardAsync())
        {
            context.PreventNavigation();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_navigationInterop is not null)
        {
            await _navigationInterop.DisposeAsync();
        }
    }

    private sealed record LinkOption(string Value, string Label, RecipeContent? Content);
    private sealed record MetricOption(string Id, string Name);
}
