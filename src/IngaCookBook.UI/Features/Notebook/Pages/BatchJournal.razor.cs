using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class BatchJournal : IAsyncDisposable
{
    [Parameter] public Guid RecipeId { get; set; }
    [Parameter] public Guid VersionId { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    private RecipeDocument? _recipe;
    private RecipeVersion? _version;
    private DateTime? _madeDate;
    private DateTime? _tastedDate;
    private BrowserDateInterop? _dateInterop;

    private bool DatesReady { get; set; }
    private bool JournalDisabled => Disabled || !DatesReady;
    private string _batchNotes = "";
    private string _batchId = "";
    private string _notes = "";
    private string _nextIdea = "";
    private List<ScoreInput> _scores = [];
    private IEnumerable<BatchOption> BatchOptions => _version?.Batches.Select((b, i) =>
        new BatchOption(b.Id.ToString(), $"Batch {i + 1} · {b.MadeAt:MMM d, yyyy}")) ?? [];

    protected override Task OnParametersSetAsync() => RunAsync(async () =>
    {
        await ReloadAsync();
        _scores = _recipe?.Metrics.Select(m => new ScoreInput { Id = m.Id, Name = m.Name }).ToList() ?? [];
    });

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dateInterop = new BrowserDateInterop(JavaScript);
            var today = await _dateInterop.GetTodayAsync();
            _madeDate ??= today;
            _tastedDate ??= today;
            DatesReady = true;
            StateHasChanged();
        }
    }

    private async Task ReloadAsync()
    {
        _recipe = await Notebook.GetRecipeAsync(RecipeId);
        _version = _recipe?.Versions.FirstOrDefault(v => v.Id == VersionId);
        if (string.IsNullOrEmpty(_batchId) && _version?.Batches.Count > 0)
        {
            _batchId = _version.Batches[^1].Id.ToString();
        }
    }

    private Task MakeAsync() => RunAsync(async () =>
    {
        if (_recipe is null || _madeDate is not { } madeDate) { return; }
        var result = await Notebook.MakeBatchAsync(RecipeId, VersionId, new(_recipe.Revision, Date(madeDate), _batchNotes));
        if (Saved(result) && result is ChangeSaved saved)
        {
            _batchId = saved.Id.ToString();
            _batchNotes = "";
            await ReloadAsync();
        }
    });

    private Task EvaluateAsync() => RunAsync(async () =>
    {
        if (_recipe is null || _tastedDate is not { } tastedDate || !Guid.TryParse(_batchId, out var batchId)) { return; }
        var request = new EvaluationRequest(_recipe.Revision, Date(tastedDate), _notes, _nextIdea,
            _scores.Select(s => new MetricScore(s.Id, s.Score, s.Notes)).ToArray());
        if (Saved(await Notebook.EvaluateAsync(RecipeId, VersionId, batchId, request)))
        {
            _notes = "";
            _nextIdea = "";
            foreach (var score in _scores) { score.Score = null; score.Notes = ""; }
            await ReloadAsync();
        }
    });

    private static DateTimeOffset Date(DateTime value) => new(DateTime.SpecifyKind(value.Date, DateTimeKind.Utc));

    public async ValueTask DisposeAsync()
    {
        if (_dateInterop is not null)
        {
            await _dateInterop.DisposeAsync();
        }
    }

    private sealed record BatchOption(string Id, string Label);
    private sealed class ScoreInput
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int? Score { get; set; }
        public string Notes { get; set; } = "";
    }
}
