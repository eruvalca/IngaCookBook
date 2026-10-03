using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class RecipeSettings
{
    [Parameter] public Guid RecipeId { get; set; }
    private RecipeDocument? _recipe;
    private string _name = "";
    private string _description = "";
    private List<MetricInput> _metrics = [];
    private bool _confirmRemoval;
    private string[] RemovedNames => GetRemovedNames();

    protected override Task OnParametersSetAsync() => RunAsync(async () =>
    {
        _recipe = await Notebook.GetRecipeAsync(RecipeId);
        if (_recipe is not null)
        {
            _name = _recipe.Name;
            _description = _recipe.Description;
            _metrics = _recipe.Metrics.Select(m => new MetricInput { Id = m.Id, Name = m.Name }).ToList();
        }
    });

    private string[] GetRemovedNames() =>
        _recipe?.Metrics.Where(m => !_metrics.Any(current => current.Id == m.Id)).Select(m => m.Name).ToArray() ?? [];

    private void AddMetric() => _metrics.Add(new());

    private Task SaveAsync() => RunAsync(async () =>
    {
        if (_recipe is null || (RemovedNames.Length > 0 && !_confirmRemoval)) { return; }
        var result = await Notebook.SaveSettingsAsync(RecipeId,
            new(_recipe.Revision, _name, _description, _metrics.Select(m => new EvaluationMetric(m.Id, m.Name)).ToArray()));
        if (Saved(result))
        {
            _recipe = await Notebook.GetRecipeAsync(RecipeId);
            _metrics = _recipe!.Metrics.Select(m => new MetricInput { Id = m.Id, Name = m.Name }).ToList();
            _confirmRemoval = false;
        }
    });

    private sealed class MetricInput
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
    }
}
