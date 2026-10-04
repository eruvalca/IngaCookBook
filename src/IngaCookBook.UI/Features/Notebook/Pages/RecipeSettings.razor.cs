using System.Text.Json;
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
    private string _coverId = "";
    private List<PhotoInput> _photos = [];
    private bool ShowPhotoSettings => string.Equals(new Uri(Navigation.Uri).Fragment, "#recipe-photos", StringComparison.Ordinal);
    private IEnumerable<PhotoOption> CoverOptions => new[] { new PhotoOption("", "Automatic · standard or first available photo") }.Concat(_photos.Select(p => new PhotoOption(p.Id.ToString(), $"V{p.VersionNumber} · {p.Caption}")));
    private string[] RemovedNames => GetRemovedNames();
    protected override string FormState => JsonSerializer.Serialize(new { _name, _description, _metrics, _confirmRemoval, _coverId, _photos });

    protected override Task OnParametersSetAsync() => LoadAsync(async ct =>
    {
        _recipe = null;
        _recipe = await ReceiveAsync(Notebook.GetRecipeAsync(RecipeId, ct), ct);
        if (_recipe is not null)
        {
            _coverId = _recipe.CoverPhotoId?.ToString() ?? "";
            _photos = _recipe.Versions.SelectMany(v => v.Photos.Select(p => new PhotoInput { Id = p.Id, VersionId = v.Id, VersionNumber = v.Number, Caption = p.Caption })).ToList();
            _name = _recipe.Name;
            _description = _recipe.Description;
            _metrics = _recipe.Metrics.Select(m => new MetricInput { Id = m.Id, Name = m.Name }).ToList();
            _confirmRemoval = false;
            CaptureSavedState();
        }
    });

    private string[] GetRemovedNames() =>
        _recipe?.Metrics.Where(m => !_metrics.Any(current => current.Id == m.Id)).Select(m => m.Name).ToArray() ?? [];

    private void AddMetric() => _metrics.Add(new());

    private Task SaveAsync() => RunAsync(async ct =>
    {
        if (_recipe is null || (RemovedNames.Length > 0 && !_confirmRemoval)) { return; }
        var result = await ReceiveAsync(Notebook.SaveSettingsAsync(RecipeId,
            new(_recipe.Revision, _name, _description, _metrics.Select(m => new EvaluationMetric(m.Id, m.Name)).ToArray(), Guid.TryParse(_coverId, out var cover) ? cover : null, _photos.ToDictionary(p => p.Id, p => p.Caption)), ct), ct);
        if (Saved(result))
        {
            _recipe = await ReceiveAsync(Notebook.GetRecipeAsync(RecipeId, ct), ct);
            _metrics = _recipe!.Metrics.Select(m => new MetricInput { Id = m.Id, Name = m.Name }).ToList();
            _confirmRemoval = false;
            await AcceptChangesAsync(ct);
        }
    });

    private sealed record PhotoOption(string Id, string Label);
    private sealed class PhotoInput
    {
        public Guid Id { get; init; }
        public int VersionNumber { get; init; }
        public Guid VersionId { get; init; }
        public string Caption { get; set; } = "";
    }

    private sealed class MetricInput
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
    }
}
